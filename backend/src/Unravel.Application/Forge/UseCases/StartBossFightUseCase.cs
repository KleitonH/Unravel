using System.Text.Json;
using Unravel.Application.Forge.BossFight;
using Unravel.Application.Forge.DTOs;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Forge;

namespace Unravel.Application.Forge.UseCases;

/// <summary>
/// PR 50 — monta uma sessão de Boss Fight pra uma trilha. Valida
/// desbloqueio, seleciona N perguntas via <see cref="BossFightSelector"/>
/// e retorna pacote pronto pra UI renderizar.
///
/// <para><b>Desbloqueio</b>: todas as ilhas regulares (Status=Completed)
/// devem estar conquistadas. Sem isso retorna <c>Unlocked=false</c> com
/// <c>LockReason</c> populado.</para>
/// </summary>
public sealed class StartBossFightUseCase
{
    public const int BossFightQuestionCount = BossFightSelector.DefaultQuestionCount;
    public const int PassThreshold          = 7;   // 70% de 10

    private readonly IBossFightRepository          _repo;
    private readonly IGeneratedChallengeRepository _generatedRepo;
    private readonly IUserSeenChallengeRepository  _seen;

    public StartBossFightUseCase(
        IBossFightRepository          repo,
        IGeneratedChallengeRepository generatedRepo,
        IUserSeenChallengeRepository  seen)
    {
        _repo          = repo;
        _generatedRepo = generatedRepo;
        _seen          = seen;
    }

    public async Task<BossFightStartResponse?> ExecuteAsync(
        Guid userId, int trailId, CancellationToken ct = default)
    {
        var trail = await _repo.GetTrailMetaAsync(trailId, ct);
        if (trail is null) return null;

        var record = await _repo.GetUserBossFightAsync(userId, trailId, ct);

        var missingCount = await _repo.GetIncompleteContentsCountAsync(userId, trailId, ct);
        if (missingCount > 0)
            return new BossFightStartResponse(
                trail.Id, trail.Name, Unlocked: false,
                LockReason: $"Complete as {missingCount} ilha(s) restante(s) pra liberar o desafio final.",
                PassThreshold: PassThreshold, TotalQuestions: 0,
                AttemptCount: record?.AttemptCount ?? 0,
                BestScore:    record?.BestScore    ?? 0,
                FirstWonAt:   record?.FirstWonAt,
                Questions:    Array.Empty<PoolChallengeDto>());

        var pool = await _repo.GetTrailPoolAsync(trailId, ct);
        if (pool.Count == 0)
            return new BossFightStartResponse(
                trail.Id, trail.Name, Unlocked: false,
                LockReason: "Trilha sem perguntas geradas. Volte mais tarde.",
                PassThreshold: PassThreshold, TotalQuestions: 0,
                AttemptCount: record?.AttemptCount ?? 0,
                BestScore:    record?.BestScore    ?? 0,
                FirstWonAt:   record?.FirstWonAt,
                Questions:    Array.Empty<PoolChallengeDto>());

        var topicIds = pool.Select(g => g.TopicId).Distinct().OrderBy(t => t).ToList();
        var seenIds  = await _seen.GetSeenIdsAsync(userId, pool.Select(g => g.Id).ToList(), ct);

        var candidates = pool.Select(g => new BossCandidate(
            g.Id, g.TopicId, g.Strategy.ToString(),
            g.EstimatedDifficulty, g.CorrectRate, g.ServedCount)).ToList();

        var choices = BossFightSelector.Select(
            topicIds, candidates, seenIds, BossFightQuestionCount);

        if (choices.Count == 0)
            return new BossFightStartResponse(
                trail.Id, trail.Name, Unlocked: true, LockReason: null,
                PassThreshold: PassThreshold,
                TotalQuestions: 0,
                AttemptCount: record?.AttemptCount ?? 0,
                BestScore:    record?.BestScore    ?? 0,
                FirstWonAt:   record?.FirstWonAt,
                Questions:    Array.Empty<PoolChallengeDto>());

        var chosenById = pool.Where(g => choices.Any(c => c.Id == g.Id))
                             .ToDictionary(g => g.Id);
        var questions = choices.Select(c => EntityToDto(chosenById[c.Id])).ToList();

        await _generatedRepo.IncrementServedAsync(choices.Select(c => c.Id), ct);

        return new BossFightStartResponse(
            TrailId:        trail.Id,
            TrailName:      trail.Name,
            Unlocked:       true,
            LockReason:     null,
            PassThreshold:  PassThreshold,
            TotalQuestions: questions.Count,
            AttemptCount:   record?.AttemptCount ?? 0,
            BestScore:      record?.BestScore    ?? 0,
            FirstWonAt:     record?.FirstWonAt,
            Questions:      questions);
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
                                         options, correctIndex, explanation,
                                         g.EstimatedDifficulty, g.ContentId);
        }
        catch (JsonException)
        {
            return new PoolChallengeDto(g.Id, g.Strategy.ToString(),
                "[pergunta corrompida; favor reportar]",
                new[] { "—" }, 0, null, g.EstimatedDifficulty, g.ContentId);
        }
    }
}
