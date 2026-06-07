using System.Text.Json;
using Unravel.Application.Forge.DTOs;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Forge.UseCases;

/// <summary>
/// Monta um pool de perguntas geradas (<see cref="GeneratedChallenge"/>)
/// para um Content específico, calibrado para o nível de domínio do
/// usuário no Topic correspondente.
///
/// <para><b>Fluxo</b>:</para>
/// <list type="number">
///   <item>Lê o Content e o grafo da trilha.</item>
///   <item>Lê a mastery do usuário no topic do Content (defaulta cold-start).</item>
///   <item>Busca perguntas geradas já persistidas, ordenadas por
///   <c>ServedCount</c> ascendente.</item>
///   <item>Se faltar para atingir <c>targetCount</c>, roda o Forge,
///   persiste os novos drafts (com BodyJson serializado) e os inclui.</item>
///   <item>Incrementa <c>ServedCount</c> dos selecionados.</item>
/// </list>
///
/// <para>Não consulta <c>Challenge</c> curado: hoje <c>Challenge</c> está
/// ligado a <c>Trail</c>, não a <c>Content</c>. Mesclar exigiria
/// <c>TopicResolver</c> por Challenge curado a cada chamada — caro e
/// indireto. Quando uma migration adicionar <c>Challenge.ContentId</c>,
/// este use case ganha mesclagem trivialmente.</para>
/// </summary>
public sealed class GetChallengePoolUseCase
{
    private readonly IForgeReadModel                  _readModel;
    private readonly IKnowledgeGraphCache             _graphCache;
    private readonly IMasteryRepository               _masteryRepo;
    private readonly IGeneratedChallengeRepository    _generatedRepo;
    private readonly IClaimExtractor                  _claimExtractor;
    private readonly IQuestionForgeQueue              _forgeQueue;

    /// <summary>PR 51 — recebe <see cref="IClaimExtractor"/> + <see cref="IQuestionForgeQueue"/>
    /// pra disparar jobs urgent LlmGrounded quando o pool está curto,
    /// substituindo o antigo fallback template-based (Cloze/Definition
    /// tinham bugs de qualidade documentados).</summary>
    public GetChallengePoolUseCase(
        IForgeReadModel               readModel,
        IKnowledgeGraphCache          graphCache,
        IMasteryRepository            masteryRepo,
        IGeneratedChallengeRepository generatedRepo,
        IClaimExtractor               claimExtractor,
        IQuestionForgeQueue           forgeQueue)
    {
        _readModel      = readModel;
        _graphCache     = graphCache;
        _masteryRepo    = masteryRepo;
        _generatedRepo  = generatedRepo;
        _claimExtractor = claimExtractor;
        _forgeQueue     = forgeQueue;
    }

    public async Task<ChallengePoolResponse?> ExecuteAsync(
        Guid userId, int contentId, int targetCount, CancellationToken ct = default)
    {
        if (targetCount is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(targetCount), "targetCount deve estar em [1,20]");

        var content = await _readModel.GetContentAsync(contentId, ct);
        if (content is null) return null;

        var graph = await _graphCache.GetOrBuildAsync(content.TrailId, ct);
        var topic = graph.Topics.FirstOrDefault(t => t.ContentId == contentId);
        if (topic is null)
            // Content ativo mas grafo não tem o topic — pode acontecer em
            // janela de cache desatualizado. Invalida e tenta uma vez.
            return new ChallengePoolResponse(contentId, content.Title, content.TrailId,
                TargetUserMastery: 0, Challenges: Array.Empty<PoolChallengeDto>());

        var mastery = await _masteryRepo.GetAsync(userId, topic.Id, ct);
        var userMastery = mastery is null
            ? 0.0
            : MasteryScoring.EffectiveScore(mastery, DateTime.UtcNow);

        // 1. tentar servir do que já está persistido.
        var existing = await _generatedRepo.GetByContentAsync(contentId, ct);

        // 2. PR 51 — se faltar, NÃO geramos template-based (causavam Cloze/
        //    Definition com bugs de qualidade — vide pergunta "O que é O
        //    componente?" e similares). Em vez disso, enfileiramos jobs
        //    urgent LlmGrounded pro pool ficar melhor na próxima sessão.
        //    Aluno recebe pool curto agora — preferível a misturar com lixo.
        var missing = targetCount - existing.Count;
        var moreComing  = false;
        if (missing > 0)
        {
            try
            {
                var claims = _claimExtractor.Extract(content.Body)
                    .OrderByDescending(c => c.Score)
                    .Take(Math.Max(missing, 5)) // pelo menos 5 jobs pra dar margem ao yield
                    .ToList();
                if (claims.Count > 0)
                {
                    var enqueued = await _forgeQueue.EnqueueForContentAsync(
                        contentId, claims, ForgeJobPriority.Urgent, ct);
                    moreComing = enqueued > 0;
                }
            }
            catch
            {
                // Best-effort. Falhar aqui não pode bloquear o quiz —
                // aluno recebe o pool curto sem warning. Próxima sessão
                // (cron diário) recupera o pool naturalmente.
            }
        }

        // 3. seleção final: ordenada por proximidade da targetUserMastery + 0.15,
        //    cortando em targetCount. Estável (ToList preserva ordem).
        var target = Math.Clamp(userMastery + 0.15, 0.10, 0.95);
        var selected = existing
            .OrderBy(g => Math.Abs(g.EstimatedDifficulty - target))
            .ThenBy(g => g.ServedCount)
            .ThenBy(g => g.Id)
            .Take(targetCount)
            .ToList();

        // 4. incrementa contadores.
        await _generatedRepo.IncrementServedAsync(selected.Select(g => g.Id), ct);

        return new ChallengePoolResponse(
            ContentId:         contentId,
            ContentTitle:      content.Title,
            TrailId:           content.TrailId,
            TargetUserMastery: Math.Round(userMastery, 4),
            Challenges:        selected.Select(EntityToDto).ToList(),
            MoreComing:        moreComing);
    }

    // ── Mapeamentos ──────────────────────────────────────────────────

    private static GeneratedChallenge DraftToEntity(GeneratedChallengeDraft d, int trailId)
    {
        var body = new
        {
            options       = d.Options,
            correctIndex  = d.CorrectIndex,
            explanation   = d.Explanation,
        };
        return new GeneratedChallenge
        {
            ContentId           = d.SourceContentId,
            TopicId             = d.SourceTopicId,
            TrailId             = trailId,
            Strategy            = d.Strategy,
            Prompt              = d.Prompt,
            BodyJson            = JsonSerializer.Serialize(body),
            EstimatedDifficulty = d.EstimatedDifficulty,
            ServedCount         = 0,
            CorrectRate         = 0,
            IsActive            = true,
            CreatedAt           = DateTime.UtcNow,
        };
    }

    private static PoolChallengeDto EntityToDto(GeneratedChallenge g)
    {
        // Parse defensivo: documento JSON existe e é estável (gerado por nós),
        // mas vale evitar exceção que derrube o endpoint inteiro por uma linha
        // corrompida.
        try
        {
            var doc           = JsonDocument.Parse(g.BodyJson);
            var options       = doc.RootElement.GetProperty("options").EnumerateArray()
                                                .Select(e => e.GetString() ?? "").ToList();
            var correctIndex  = doc.RootElement.GetProperty("correctIndex").GetInt32();
            var explanation   = doc.RootElement.TryGetProperty("explanation", out var ex)
                                  ? ex.GetString() : null;
            // PR 34a — shape opcional no BodyJson. Rows antigas (sem o campo)
            // ficam como "MultipleChoice" pra UI renderizar do jeito clássico.
            var shape         = doc.RootElement.TryGetProperty("shape", out var sh)
                                  ? sh.GetString() ?? "MultipleChoice"
                                  : "MultipleChoice";
            return new PoolChallengeDto(g.Id, g.Strategy.ToString(), g.Prompt,
                                         options, correctIndex, explanation, g.EstimatedDifficulty, g.ContentId,
                                         Shape: shape);
        }
        catch (JsonException)
        {
            return new PoolChallengeDto(g.Id, g.Strategy.ToString(),
                "[pergunta corrompida; favor reportar]",
                new[] { "—" }, 0, null, g.EstimatedDifficulty, g.ContentId,
                Shape: "MultipleChoice");
        }
    }
}
