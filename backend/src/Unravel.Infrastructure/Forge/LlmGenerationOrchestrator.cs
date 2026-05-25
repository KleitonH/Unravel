using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Forge;

/// <summary>
/// Lote noturno: itera todos os <c>Content</c>s ativos com pool de
/// <c>GeneratedChallenge</c> abaixo de <c>minPoolSize</c>, pede ao
/// <see cref="IChallengeForge"/> que gere <c>targetPerContent</c> drafts
/// (todas as strategies registradas — incluindo LLM se ativa) e persiste.
///
/// <para>Idempotente: rodar 2x na mesma noite só adiciona drafts onde
/// ainda faltam. Conservativo: pula contents já bem servidos.</para>
///
/// <para>Logs e métricas via <see cref="Application.Telemetry.UnravelMetrics"/>
/// (PR 19) — mesmo que aqui não cravamos counters novos, o ChallengeForge
/// já instrumenta drafts/aprovados/rejeitados por strategy.</para>
/// </summary>
public sealed class LlmGenerationOrchestrator : ILlmGenerationOrchestrator
{
    private readonly ApplicationDbContext _db;
    private readonly IKnowledgeGraphCache _graphCache;
    private readonly IChallengeForge _forge;
    private readonly IGeneratedChallengeRepository _repo;
    private readonly ILogger<LlmGenerationOrchestrator> _log;

    public LlmGenerationOrchestrator(
        ApplicationDbContext db,
        IKnowledgeGraphCache graphCache,
        IChallengeForge forge,
        IGeneratedChallengeRepository repo,
        ILogger<LlmGenerationOrchestrator> log)
    {
        _db         = db;
        _graphCache = graphCache;
        _forge      = forge;
        _repo       = repo;
        _log        = log;
    }

    public async Task<LlmGenerationReport> RunAsync(
        int minPoolSize = 5,
        int targetPerContent = 8,
        CancellationToken ct = default)
    {
        var sw         = Stopwatch.StartNew();
        var scanned    = 0;
        var augmented  = 0;
        var draftsAdded = 0;
        var failures   = 0;

        // Lista de Content ativos com tamanho do pool atual (LEFT JOIN agregado).
        var pendingContents = await (
            from c in _db.Content.AsNoTracking()
            where c.IsActive
            let poolCount = _db.GeneratedChallenge.Count(g => g.ContentId == c.Id && g.IsActive)
            where poolCount < minPoolSize
            select new { Content = c, PoolCount = poolCount }
        ).ToListAsync(ct);

        _log.LogInformation(
            "LLM generation cycle: {Count} contents with pool < {Min} (target per content: {Target})",
            pendingContents.Count, minPoolSize, targetPerContent);

        foreach (var item in pendingContents)
        {
            ct.ThrowIfCancellationRequested();
            scanned++;

            try
            {
                var graph = await _graphCache.GetOrBuildAsync(item.Content.TrailId, ct);
                if (!graph.Topics.Any(t => t.ContentId == item.Content.Id))
                {
                    _log.LogDebug("Content {ContentId} sem topic no grafo; pulando.", item.Content.Id);
                    continue;
                }

                // Forge corre todas strategies (LLM inclusa se DI registrou).
                // targetPerContent define o lote por content; targetUserMastery
                // genérica = 0.4 (não há user "alvo" no batch, calibragem fica
                // por user no momento de servir).
                var drafts = _forge.Build(
                    item.Content, graph, targetPerContent, targetUserMastery: 0.4);

                if (drafts.Count == 0) continue;

                var entities = drafts.Select(d => DraftToEntity(d, item.Content.TrailId)).ToList();
                await _repo.AddManyAsync(entities, ct);

                augmented++;
                draftsAdded += entities.Count;
            }
            catch (Exception ex)
            {
                failures++;
                _log.LogWarning(ex,
                    "Falha gerando para Content {ContentId}; segue o lote.",
                    item.Content.Id);
            }
        }

        sw.Stop();
        var report = new LlmGenerationReport(
            ContentsScanned:   scanned,
            ContentsAugmented: augmented,
            DraftsAdded:       draftsAdded,
            Failures:          failures,
            Duration:          sw.Elapsed);

        _log.LogInformation("LLM generation cycle done: {Report}", report);
        return report;
    }

    private static GeneratedChallenge DraftToEntity(GeneratedChallengeDraft d, int trailId)
    {
        var body = new
        {
            options      = d.Options,
            correctIndex = d.CorrectIndex,
            explanation  = d.Explanation,
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
}
