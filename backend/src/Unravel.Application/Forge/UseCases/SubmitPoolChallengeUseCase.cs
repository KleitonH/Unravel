using System.Diagnostics;
using System.Text.Json;
using Unravel.Application.Forge.DTOs;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Gamification.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Application.Telemetry;
using Unravel.Domain.Gamification;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Forge.UseCases;

/// <summary>
/// Submete uma resposta a uma <c>GeneratedChallenge</c> e propaga o sinal
/// para 3 sistemas:
///
/// <list type="number">
///   <item><b>Mastery</b> do tópico — via <see cref="MasteryScoring.Apply"/>
///   (PR 13).</item>
///   <item><b>Gamificação</b> do usuário — XP/Coins/Stars/Vidas via
///   <see cref="RewardCalculator"/> + <see cref="IUserGamificationGateway"/>
///   (PR 15). Streak atualizado dentro do gateway (cada submit = atividade).</item>
///   <item><b>Estatística da pergunta</b> — incrementa ServedCount e
///   recalcula CorrectRate (PR 13).</item>
/// </list>
///
/// <para>Servidor é a fonte da verdade — o cliente nunca decide se acertou
/// nem quantos pontos ganhou.</para>
/// </summary>
public sealed class SubmitPoolChallengeUseCase
{
    private readonly IGeneratedChallengeRepository _generated;
    private readonly IMasteryRepository            _mastery;
    private readonly IUserGamificationGateway      _gamification;

    public SubmitPoolChallengeUseCase(
        IGeneratedChallengeRepository generated,
        IMasteryRepository            mastery,
        IUserGamificationGateway      gamification)
    {
        _generated    = generated;
        _mastery      = mastery;
        _gamification = gamification;
    }

    public async Task<SubmitPoolChallengeResponse?> ExecuteAsync(
        Guid userId, int contentId, SubmitPoolChallengeRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var gc = await _generated.GetByIdAsync(request.GeneratedChallengeId, ct);
        if (gc is null) return null;
        if (gc.ContentId != contentId) return null;          // 404 — defesa contra IDs trocados

        var (correctIndex, explanation) = ParseBody(gc.BodyJson);
        if (correctIndex < 0) return null;                   // BodyJson corrompido

        var isCorrect = request.SelectedOptionIndex == correctIndex;
        var outcome   = isCorrect ? 1.0 : 0.0;
        var now       = DateTime.UtcNow;

        // 1) Mastery do topic dessa pergunta.
        var current = await _mastery.GetAsync(userId, gc.TopicId, ct)
                      ?? Mastery.Initial(userId, gc.TopicId, gc.TrailId, now);
        var updated = MasteryScoring.Apply(current, outcome, now);
        await _mastery.UpsertAsync(updated, ct);

        // 2) Estatística da pergunta (média móvel + served).
        await _generated.RecordOutcomeAsync(gc.Id, isCorrect, ct);

        // 3) Gamificação — recompensas + vida + streak. Difficulty da
        //    pergunta calibra a magnitude da recompensa.
        var rewards  = RewardCalculator.Compute(gc.EstimatedDifficulty, isCorrect);
        var snapshot = await _gamification.ApplyAsync(userId, rewards, now, ct);

        // Telemetria PR 19
        UnravelMetrics.QuizSubmissions.Add(1,
            new KeyValuePair<string, object?>("outcome", isCorrect ? "correct" : "wrong"),
            new KeyValuePair<string, object?>("strategy", gc.Strategy.ToString()));
        UnravelMetrics.QuizSubmitDurationMs.Record(sw.Elapsed.TotalMilliseconds);

        return new SubmitPoolChallengeResponse(
            IsCorrect:            isCorrect,
            CorrectOptionIndex:   correctIndex,
            Explanation:          explanation,
            NewMasteryScore:      Math.Round(updated.Score, 4),
            NewMasteryConfidence: updated.Confidence,
            XpEarned:             rewards.Xp,
            CoinsEarned:          rewards.Coins,
            StarsEarned:          rewards.Stars,
            LifeDelta:            rewards.LifeDelta,
            TotalXp:              snapshot.Xp,
            TotalCoins:           snapshot.Coins,
            TotalStars:           snapshot.Stars,
            TotalLives:           snapshot.Lives,
            StreakDays:           snapshot.StreakDays);
    }

    private static (int correctIndex, string? explanation) ParseBody(string bodyJson)
    {
        try
        {
            var doc = JsonDocument.Parse(bodyJson);
            var correctIndex = doc.RootElement.GetProperty("correctIndex").GetInt32();
            var explanation  = doc.RootElement.TryGetProperty("explanation", out var ex)
                               ? ex.GetString()
                               : null;
            return (correctIndex, explanation);
        }
        catch (JsonException)
        {
            return (-1, null);
        }
    }
}
