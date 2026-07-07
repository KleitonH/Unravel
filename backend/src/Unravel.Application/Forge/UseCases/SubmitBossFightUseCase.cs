using System.Text.Json;
using Unravel.Application.Forge.DTOs;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Gamification.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Forge;
using Unravel.Domain.Gamification;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Forge.UseCases;

/// <summary>
/// PR 50 — corrige todas as respostas em batch, atualiza mastery por
/// topic, gamificação, marca UserSeenChallenge, e registra o resultado
/// em UserBossFight (singleton por user/trail).
///
/// <para><b>Por que batch e não streaming</b>: Boss é prova, não treino
/// adaptativo. Aluno responde tudo, vê resultado de uma vez. Reduz
/// chamadas N→1 e simplifica UX.</para>
///
/// <para><b>Recompensas</b>:</para>
/// <list type="bullet">
///   <item>Primeira vitória (≥ <see cref="StartBossFightUseCase.PassThreshold"/>):
///   500 XP + badge "Mestre de {TrailName}".</item>
///   <item>Vitória subsequente: 100 XP (escala).</item>
///   <item>Derrota: 50 XP de consolação.</item>
/// </list>
/// </summary>
public sealed class SubmitBossFightUseCase
{
    public const int RewardFirstWinXp = 500;
    public const int RewardRetryWinXp = 100;
    public const int RewardLossXp     = 50;

    private readonly IBossFightRepository          _repo;
    private readonly IGeneratedChallengeRepository _generatedRepo;
    private readonly IUserSeenChallengeRepository  _seen;
    private readonly IMasteryRepository            _mastery;
    private readonly IUserGamificationGateway      _gamification;
    private readonly IActivitySink?                _activity;

    public SubmitBossFightUseCase(
        IBossFightRepository          repo,
        IGeneratedChallengeRepository generatedRepo,
        IUserSeenChallengeRepository  seen,
        IMasteryRepository            mastery,
        IUserGamificationGateway      gamification)
    {
        _repo          = repo;
        _generatedRepo = generatedRepo;
        _seen          = seen;
        _mastery       = mastery;
        _gamification  = gamification;
        _activity      = null;
    }

    /// <summary>Construtor "completo" — inclui o <see cref="IActivitySink"/> pra
    /// alimentar a missão diária de Boss. É o que o DI resolve em produção.</summary>
    public SubmitBossFightUseCase(
        IBossFightRepository          repo,
        IGeneratedChallengeRepository generatedRepo,
        IUserSeenChallengeRepository  seen,
        IMasteryRepository            mastery,
        IUserGamificationGateway      gamification,
        IActivitySink                 activity)
        : this(repo, generatedRepo, seen, mastery, gamification)
    {
        _activity = activity;
    }

    public async Task<BossFightResultResponse?> ExecuteAsync(
        Guid userId, int trailId, BossFightSubmitRequest request, CancellationToken ct = default)
    {
        if (request?.Answers is null || request.Answers.Count == 0) return null;

        var trail = await _repo.GetTrailMetaAsync(trailId, ct);
        if (trail is null) return null;

        // Carrega pool completo da trilha pra resolver os ChallengeIds enviados.
        var pool          = await _repo.GetTrailPoolAsync(trailId, ct);
        var poolById      = pool.ToDictionary(g => g.Id);

        var now      = DateTime.UtcNow;
        var outcomes = new List<BossFightAnswerOutcome>(request.Answers.Count);
        var score    = 0;

        foreach (var answer in request.Answers)
        {
            if (!poolById.TryGetValue(answer.ChallengeId, out var gc)) continue;

            var (correctIndex, explanation) = ParseBody(gc.BodyJson);
            if (correctIndex < 0) continue;

            var isCorrect = answer.SelectedOptionIndex == correctIndex;
            if (isCorrect) score++;

            outcomes.Add(new BossFightAnswerOutcome(
                gc.Id, isCorrect, correctIndex, explanation));

            // Mastery por topic
            var current = await _mastery.GetAsync(userId, gc.TopicId, ct)
                          ?? Mastery.Initial(userId, gc.TopicId, gc.TrailId, now);
            var updated = MasteryScoring.Apply(current, isCorrect ? 1.0 : 0.0, now);
            await _mastery.UpsertAsync(updated, ct);

            await _generatedRepo.RecordOutcomeAsync(gc.Id, isCorrect, ct);
            await _seen.MarkAsync(userId, gc.Id, isCorrect, now, ct);
        }

        var passed = score >= StartBossFightUseCase.PassThreshold;

        var record     = await _repo.GetUserBossFightAsync(userId, trailId, ct);
        var isFirstWin = passed && record?.FirstWonAt is null;

        if (record is null)
        {
            record = new UserBossFight
            {
                UserId        = userId,
                TrailId       = trailId,
                AttemptCount  = 1,
                BestScore     = score,
                LastScore     = score,
                LastAttemptAt = now,
                FirstWonAt    = passed ? now : null,
            };
        }
        else
        {
            record.AttemptCount++;
            record.LastScore     = score;
            record.LastAttemptAt = now;
            if (score > record.BestScore)               record.BestScore  = score;
            if (passed && record.FirstWonAt is null)    record.FirstWonAt = now;
        }
        await _repo.UpsertUserBossFightAsync(record, ct);

        var xpEarned = passed
            ? (isFirstWin ? RewardFirstWinXp : RewardRetryWinXp)
            : RewardLossXp;

        var rewards = new SubmissionRewards(
            Xp: xpEarned,
            Coins: passed ? 50 : 10,
            Stars: isFirstWin ? 1 : 0,
            LifeDelta: 0);
        await _gamification.ApplyAsync(userId, rewards, now, ct);

        // Missão diária de Boss — o engine credita novelo + caixinha se a missão
        // "Enfrente 1 Boss" estiver no dia. Best-effort (o sink nunca lança).
        if (_activity is not null)
            await _activity.RecordAsync(userId, ActivityKind.BossFought, 1, now, ct);

        return new BossFightResultResponse(
            TrailId:        trail.Id,
            Score:          score,
            TotalQuestions: outcomes.Count,
            PassThreshold:  StartBossFightUseCase.PassThreshold,
            Passed:         passed,
            IsFirstWin:     isFirstWin,
            XpEarned:       xpEarned,
            BadgeAwarded:   isFirstWin ? $"Mestre de {trail.Name}" : null,
            Outcomes:       outcomes);
    }

    private static (int correctIndex, string? explanation) ParseBody(string bodyJson)
    {
        try
        {
            var doc          = JsonDocument.Parse(bodyJson);
            var correctIndex = doc.RootElement.GetProperty("correctIndex").GetInt32();
            var explanation  = doc.RootElement.TryGetProperty("explanation", out var ex)
                                  ? ex.GetString() : null;
            return (correctIndex, explanation);
        }
        catch (JsonException)
        {
            return (-1, null);
        }
    }
}
