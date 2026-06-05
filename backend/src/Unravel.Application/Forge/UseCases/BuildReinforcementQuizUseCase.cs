using System.Text.Json;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.DTOs;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Forge.UseCases;

/// <summary>
/// PR 37 — monta um "quiz de reforço" focado nas fraquezas do aluno
/// (mastery efetiva &lt; threshold) excluindo perguntas que ele já viu.
///
/// <para><b>Por que existe</b>: o quiz normal (<see cref="GetChallengePoolUseCase"/>)
/// serve perguntas pra <i>um Content</i> específico. Reforço opera no nível
/// de <i>trilha</i>, atravessando todos os contents pra atacar exatamente
/// os tópicos onde o aluno está mais fraco. Esse é o loop educacional
/// completo: <b>aluno erra → estuda → pede pra ser testado de novo focado
/// nas fraquezas</b>.</para>
///
/// <para><b>Algoritmo</b>:</para>
/// <list type="number">
///   <item>Lê masteries da trilha + calcula effective mastery (com decay).</item>
///   <item>Filtra topics com <c>effective &lt; weaknessThreshold</c>.</item>
///   <item>Sem fraquezas → retorna <c>reason=no_weaknesses</c>.</item>
///   <item>Busca perguntas ativas da trilha cujo topic está nas fraquezas.</item>
///   <item>Exclui as já vistas pelo user (anti-join via <see cref="IUserSeenChallengeRepository"/>).</item>
///   <item>Ordena por mastery do topic ASC (mais fraco primeiro) + ServedCount ASC.</item>
///   <item>Toma top <c>count</c>.</item>
///   <item>Se algum topic fraco tem pool fresco &lt; <c>minFreshPerTopic</c>,
///   enfileira jobs urgent pra repor (extrai claims do content do topic).</item>
///   <item>Increment ServedCount + retorna.</item>
/// </list>
///
/// <para><b>Stateless</b> — não persiste nenhum "snapshot do reforço".
/// Cada chamada é independente; perguntas vistas migram pra histórico via
/// hook normal do submit.</para>
/// </summary>
public sealed class BuildReinforcementQuizUseCase
{
    private readonly IKnowledgeGraphCache             _graphCache;
    private readonly IMasteryRepository               _mastery;
    private readonly IGeneratedChallengeRepository    _generated;
    private readonly IUserSeenChallengeRepository     _seen;
    private readonly IForgeReadModel                  _readModel;
    private readonly IClaimExtractor                  _claimExtractor;
    private readonly IQuestionForgeQueue              _queue;
    private readonly ILogger<BuildReinforcementQuizUseCase>? _log;

    /// <summary>Mastery efetiva abaixo disso → "fraqueza". 0.6 = 60%.
    /// Decisão data-driven: usuário típico chega entre 0.4 e 0.8 após 5
    /// quizzes; threshold em 0.6 captura "razoavelmente fraco" sem ser
    /// rigoroso demais (corte em 0.5 perderia oportunidades de reforço
    /// em tópicos onde o user oscila).</summary>
    public const double WeaknessThreshold = 0.6;

    /// <summary>Se um topic fraco tem menos que isto de perguntas frescas
    /// (não-vistas pelo user), dispara replenishment. 3 evita gerar à toa
    /// quando há margem confortável; também aceita reforços curtos com 3
    /// perguntas-mesmo-topic sem disparar geração.</summary>
    public const int MinFreshPerTopic = 3;

    /// <summary>Máximo de jobs urgent enfileirados numa única chamada.
    /// Cap defensivo — se 10 topics fracos têm pool 0, sem cap seriam
    /// 200 jobs OpenAI de uma vez (~$0.12 imediato). Cap em 5 contents
    /// = ~$0.06, dilui carga.</summary>
    public const int MaxReplenishContentsPerCall = 5;

    public BuildReinforcementQuizUseCase(
        IKnowledgeGraphCache          graphCache,
        IMasteryRepository            mastery,
        IGeneratedChallengeRepository generated,
        IUserSeenChallengeRepository  seen,
        IForgeReadModel               readModel,
        IClaimExtractor               claimExtractor,
        IQuestionForgeQueue           queue,
        ILogger<BuildReinforcementQuizUseCase>? log = null)
    {
        _graphCache     = graphCache;
        _mastery        = mastery;
        _generated      = generated;
        _seen           = seen;
        _readModel      = readModel;
        _claimExtractor = claimExtractor;
        _queue          = queue;
        _log            = log;
    }

    public async Task<ReinforcementQuizResponse> ExecuteAsync(
        Guid userId, int trailId, int count, CancellationToken ct = default)
    {
        if (count is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(count), "count deve estar em [1,20]");

        var now      = DateTime.UtcNow;
        var graph    = await _graphCache.GetOrBuildAsync(trailId, ct);
        var topicsById = graph.Topics.ToDictionary(t => t.Id);

        // 1. Effective mastery por topic da trilha.
        var rawMasteries = await _mastery.GetByTrailAsync(userId, trailId, ct);
        var effective    = rawMasteries
            .Where(m => topicsById.ContainsKey(m.TopicId))   // ignora masteries órfãs
            .ToDictionary(
                m => m.TopicId,
                m => MasteryScoring.EffectiveScore(m, now));

        // 2 + 3. Fraquezas — topics com effective < threshold.
        // Topics nunca vistos NÃO contam como fraqueza (effective = 0
        // mas user não interagiu ainda — reforço seria gerar exemplo do
        // zero, papel do quiz normal, não do reforço).
        var weakTopicIds = effective
            .Where(kv => kv.Value < WeaknessThreshold)
            .Select(kv => kv.Key)
            .ToList();

        if (weakTopicIds.Count == 0)
        {
            return new ReinforcementQuizResponse(
                TrailId:      trailId,
                WeakTopics:   Array.Empty<WeakTopicDto>(),
                Challenges:   Array.Empty<PoolChallengeDto>(),
                MoreComing:   false,
                JobsEnqueued: 0,
                Reason:       "no_weaknesses");
        }

        // 4 + 5. Pool dos topics fracos, menos os já vistos.
        var pool        = await _generated.GetByTrailAndTopicsAsync(trailId, weakTopicIds, ct);
        var seenIds     = await _seen.GetSeenIdsAsync(userId, pool.Select(g => g.Id).ToList(), ct);
        var freshPool   = pool.Where(g => !seenIds.Contains(g.Id)).ToList();

        // 6 + 7. Seleciona top N priorizando topics mais fracos.
        var selected = freshPool
            .OrderBy(g => effective.GetValueOrDefault(g.TopicId, 0.0))   // mastery ASC
            .ThenBy(g => g.ServedCount)
            .ThenBy(g => g.Id)
            .Take(count)
            .ToList();

        // 8. Replenishment: pra cada topic fraco com freshPool < MinFreshPerTopic,
        // enfileira jobs urgent. Cap em MaxReplenishContentsPerCall pra
        // não estourar custo OpenAI numa request.
        var jobsEnqueued = 0;
        var replenished  = 0;
        var freshByTopic = freshPool.GroupBy(g => g.TopicId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var topicId in weakTopicIds.OrderBy(tid => effective.GetValueOrDefault(tid, 0.0)))
        {
            if (replenished >= MaxReplenishContentsPerCall) break;
            var freshCount = freshByTopic.GetValueOrDefault(topicId, 0);
            if (freshCount >= MinFreshPerTopic) continue;

            var topic = topicsById[topicId];
            var contentId = topic.ContentId;
            if (contentId <= 0) continue;   // topic sem content associado (defensivo)

            var content = await _readModel.GetContentAsync(contentId, ct);
            if (content is null) continue;

            var claims = _claimExtractor.Extract(content.Body)
                .OrderByDescending(c => c.Score)
                .Take(10)   // 10 jobs por content é um lote moderado
                .ToList();
            if (claims.Count == 0) continue;

            var added = await _queue.EnqueueForContentAsync(
                contentId, claims, ForgeJobPriority.Urgent, ct);
            jobsEnqueued += added;
            replenished++;
            _log?.LogInformation(
                "Reinforcement replenishment: user={UserId} topic={TopicId} content={ContentId} jobs={Jobs}",
                userId, topicId, contentId, added);
        }

        // 9. Increment served count das selecionadas (mesmo flow do quiz normal).
        if (selected.Count > 0)
            await _generated.IncrementServedAsync(selected.Select(g => g.Id), ct);

        // Reason quando há fraqueza mas pool esgotou e nenhuma fresh disponível.
        string? reason = null;
        if (selected.Count == 0 && jobsEnqueued > 0) reason = "pool_exhausted";
        if (selected.Count == 0 && jobsEnqueued == 0) reason = "no_content_for_weakness";

        return new ReinforcementQuizResponse(
            TrailId:      trailId,
            WeakTopics:   weakTopicIds.Select(tid => new WeakTopicDto(
                              TopicId:            tid,
                              TopicSlug:          topicsById[tid].Slug,
                              EffectiveMastery:   Math.Round(effective.GetValueOrDefault(tid, 0.0), 4),
                              QuestionsAvailable: freshByTopic.GetValueOrDefault(tid, 0)))
                          .OrderBy(t => t.EffectiveMastery)
                          .ToList(),
            Challenges:   selected.Select(EntityToDto).ToList(),
            MoreComing:   jobsEnqueued > 0,
            JobsEnqueued: jobsEnqueued,
            Reason:       reason);
    }

    private static PoolChallengeDto EntityToDto(GeneratedChallenge g)
    {
        // Mesma deserialização do GetChallengePoolUseCase — duplicada
        // intencionalmente pra manter use cases auto-contidos. Se virar
        // um terceiro consumidor, extrair pra helper compartilhado.
        try
        {
            var doc          = JsonDocument.Parse(g.BodyJson);
            var options      = doc.RootElement.GetProperty("options").EnumerateArray()
                                              .Select(e => e.GetString() ?? "").ToList();
            var correctIndex = doc.RootElement.GetProperty("correctIndex").GetInt32();
            var explanation  = doc.RootElement.TryGetProperty("explanation", out var ex)
                                  ? ex.GetString() : null;
            return new PoolChallengeDto(g.Id, g.Strategy.ToString(), g.Prompt,
                                         options, correctIndex, explanation, g.EstimatedDifficulty, g.ContentId);
        }
        catch (JsonException)
        {
            return new PoolChallengeDto(g.Id, g.Strategy.ToString(),
                "[pergunta corrompida; favor reportar]",
                new[] { "—" }, 0, null, g.EstimatedDifficulty, g.ContentId);
        }
    }
}
