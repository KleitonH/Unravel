using System.Text.Json;
using Unravel.Application.Forge.Adaptive;
using Unravel.Application.Forge.DTOs;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Forge.UseCases;

/// <summary>
/// PR 42 — orquestra a sessão CAT-lite:
/// <list type="number">
///   <item>Carrega Content + pool atual via <see cref="IForgeReadModel"/>.</item>
///   <item>Estima <i>ability inicial</i> a partir da Mastery do tópico
///   (effective score com decay).</item>
///   <item>Atualiza ability online com o histórico da sessão via
///   <see cref="AdaptiveSelector.EstimateAbility"/>.</item>
///   <item>Verifica critério de parada
///   (<see cref="AdaptiveSelector.ShouldStop"/>) — se positivo retorna
///   <c>done</c>.</item>
///   <item>Seleciona próxima pergunta via
///   <see cref="AdaptiveSelector.SelectNextChallengeId"/>.</item>
/// </list>
///
/// <para><b>Stateless</b>: cliente envia histórico em cada request. Sem
/// locks, sem sessão server-side, debugável (mesmo input → mesmo output).</para>
/// </summary>
public sealed class SelectNextAdaptiveChallengeUseCase
{
    private readonly IForgeReadModel               _readModel;
    private readonly IKnowledgeGraphCache          _graphCache;
    private readonly IMasteryRepository            _masteryRepo;
    private readonly IGeneratedChallengeRepository _generatedRepo;

    public SelectNextAdaptiveChallengeUseCase(
        IForgeReadModel               readModel,
        IKnowledgeGraphCache          graphCache,
        IMasteryRepository            masteryRepo,
        IGeneratedChallengeRepository generatedRepo)
    {
        _readModel     = readModel;
        _graphCache    = graphCache;
        _masteryRepo   = masteryRepo;
        _generatedRepo = generatedRepo;
    }

    public async Task<AdaptiveNextResponse?> ExecuteAsync(
        Guid userId, int contentId,
        IReadOnlyList<AdaptiveOutcome> history,
        CancellationToken ct = default)
    {
        var content = await _readModel.GetContentAsync(contentId, ct);
        if (content is null) return null;

        var graph = await _graphCache.GetOrBuildAsync(content.TrailId, ct);
        var topic = graph.Topics.FirstOrDefault(t => t.ContentId == contentId);
        if (topic is null)
            return new AdaptiveNextResponse(
                Done: true, StopReason: nameof(AdaptiveStopReason.PoolExhausted),
                AbilityEstimate: AdaptiveSelector.DefaultStartAbility,
                Question: null, QuestionsAnswered: history.Count);

        // 1. Ability inicial = effective mastery atual no topic (com decay).
        //    Sem mastery → DefaultStartAbility (cold-start neutro).
        var mastery = await _masteryRepo.GetAsync(userId, topic.Id, ct);
        var startAbility = mastery is null
            ? AdaptiveSelector.DefaultStartAbility
            : MasteryScoring.EffectiveScore(mastery, DateTime.UtcNow);

        // 2. Atualiza ability online com base no histórico da sessão.
        var ability = AdaptiveSelector.EstimateAbility(history, startAbility);

        // 3. Checa parada antes de selecionar — economiza I/O.
        var stop = AdaptiveSelector.ShouldStop(history, startAbility);
        if (stop is not null)
            return new AdaptiveNextResponse(
                Done: true, StopReason: stop.Value.ToString(),
                AbilityEstimate: Math.Round(ability, 4),
                Question: null, QuestionsAnswered: history.Count);

        // 4. Pool ativo do content (já ordenado por ServedCount asc).
        var pool = await _generatedRepo.GetByContentAsync(contentId, ct);
        var candidates = pool
            .Select(g => new AdaptiveCandidate(g.Id, g.EstimatedDifficulty, g.ServedCount))
            .ToList();

        var seen = history.Select(h => h.ChallengeId).ToHashSet();
        var nextId = AdaptiveSelector.SelectNextChallengeId(ability, candidates, seen);

        if (nextId is null)
            return new AdaptiveNextResponse(
                Done: true, StopReason: nameof(AdaptiveStopReason.PoolExhausted),
                AbilityEstimate: Math.Round(ability, 4),
                Question: null, QuestionsAnswered: history.Count);

        // 5. Materializa o challenge escolhido em DTO (reusa parse do BodyJson).
        var chosen = pool.First(g => g.Id == nextId.Value);
        await _generatedRepo.IncrementServedAsync(new[] { chosen.Id }, ct);

        return new AdaptiveNextResponse(
            Done: false, StopReason: null,
            AbilityEstimate: Math.Round(ability, 4),
            Question: EntityToDto(chosen),
            QuestionsAnswered: history.Count);
    }

    private static PoolChallengeDto EntityToDto(GeneratedChallenge g)
    {
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
