using Unravel.Domain.Knowledge;

namespace Unravel.Tests.Knowledge;

public class MasteryScoringTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Mastery Initial() =>
        Mastery.Initial(Guid.NewGuid(), topicId: 1, trailId: 1, asOf: T0);

    // ── EWMA ─────────────────────────────────────────────────────────

    [Fact]
    public void Apply_FirstAttemptPerfect_MovesScoreUpButNotToOne()
    {
        var after = MasteryScoring.Apply(Initial(), outcome: 1.0, asOf: T0);
        // alpha máximo é 0.5 → score = 0.5 * 1 + 0.5 * 0 = 0.5
        Assert.Equal(0.5, after.Score, precision: 3);
        Assert.Equal(1, after.Confidence);
    }

    [Fact]
    public void Apply_RepeatedPerfectAttempts_AsymptotesTowardOne()
    {
        var m = Initial();
        for (var i = 0; i < 20; i++)
            m = MasteryScoring.Apply(m, outcome: 1.0, asOf: T0.AddMinutes(i));

        Assert.InRange(m.Score, 0.95, 1.0);
        Assert.Equal(20, m.Confidence);
    }

    [Fact]
    public void Apply_AlphaShrinksWithConfidence_LaterAttemptsMoveLess()
    {
        var m1 = MasteryScoring.Apply(Initial(), outcome: 0.0, asOf: T0);
        // depois de muitas tentativas perfeitas, o score deve resistir a 1 outcome ruim
        var m2 = Initial();
        for (var i = 0; i < 50; i++)
            m2 = MasteryScoring.Apply(m2, outcome: 1.0, asOf: T0);
        var dropFromStable = m2.Score - MasteryScoring.Apply(m2, outcome: 0.0, asOf: T0).Score;

        // a queda de 1 zero depois de muitas perfeitas deve ser bem menor
        // que a movimentação de uma tentativa do estado inicial
        Assert.True(dropFromStable < 0.2,
            $"depois de 50 perfeitas, 1 erro derruba {dropFromStable:F3}; esperava < 0.20");
    }

    // ── SM-2 ─────────────────────────────────────────────────────────

    [Fact]
    public void Apply_GoodOutcome_PromotesInterval_AndRaisesEase()
    {
        var after = MasteryScoring.Apply(Initial(), outcome: 0.9, asOf: T0);
        Assert.Equal(3, after.SrsIntervalDays);  // primeira boa → 3 dias
        Assert.True(after.EaseFactor > 2.5);
    }

    [Fact]
    public void Apply_PartialOutcome_KeepsInterval()
    {
        var initial = Initial();
        var after   = MasteryScoring.Apply(initial, outcome: 0.5, asOf: T0);
        Assert.Equal(initial.SrsIntervalDays, after.SrsIntervalDays);
        Assert.Equal(initial.EaseFactor, after.EaseFactor);
    }

    [Fact]
    public void Apply_BadOutcome_ResetsIntervalAndPenalizesEase()
    {
        var m = MasteryScoring.Apply(Initial(), outcome: 0.9,  asOf: T0);
        m     = MasteryScoring.Apply(m,         outcome: 0.95, asOf: T0.AddDays(3));
        // agora o intervalo subiu — submeter ruim deve resetar para 1
        var bad = MasteryScoring.Apply(m, outcome: 0.1, asOf: T0.AddDays(10));

        Assert.Equal(1, bad.SrsIntervalDays);
        Assert.True(bad.EaseFactor < m.EaseFactor);
        Assert.True(bad.EaseFactor >= 1.3, "ease should not fall below floor");
    }

    [Fact]
    public void Apply_EaseFactor_CappedBelow28()
    {
        var m = Initial();
        for (var i = 0; i < 30; i++)
            m = MasteryScoring.Apply(m, outcome: 1.0, asOf: T0.AddDays(i));
        Assert.True(m.EaseFactor <= 2.8 + 1e-9);
    }

    // ── Decaimento ───────────────────────────────────────────────────

    [Fact]
    public void EffectiveScore_AtHalfLife_HalvesScore()
    {
        var m = Initial();
        m.Score = 0.8;
        var eff = MasteryScoring.EffectiveScore(m, T0.AddDays(MasteryScoring.ForgettingHalfLifeDays));
        Assert.Equal(0.4, eff, precision: 3);
    }

    [Fact]
    public void EffectiveScore_AtSameInstant_ReturnsRawScore()
    {
        var m = Initial();
        m.Score = 0.6;
        Assert.Equal(0.6, MasteryScoring.EffectiveScore(m, T0), precision: 10);
    }

    [Fact]
    public void EffectiveScore_NeverGoesNegative_EvenForPastDates()
    {
        // se asOf for anterior ao LastSeenAt (clock skew), não amplificar score
        var m = Initial();
        m.Score = 0.7;
        var eff = MasteryScoring.EffectiveScore(m, T0.AddDays(-5));
        Assert.Equal(0.7, eff, precision: 10);
    }

    // ── IsDueForReview ───────────────────────────────────────────────

    [Fact]
    public void IsDueForReview_FlipsAtNextDueDate()
    {
        var m = MasteryScoring.Apply(Initial(), outcome: 0.9, asOf: T0); // interval=3
        Assert.False(MasteryScoring.IsDueForReview(m, T0.AddDays(2.9)));
        Assert.True(MasteryScoring.IsDueForReview(m, T0.AddDays(3.0)));
        Assert.True(MasteryScoring.IsDueForReview(m, T0.AddDays(30)));
    }

    // ── Determinismo / pureza ────────────────────────────────────────

    [Fact]
    public void Apply_DoesNotMutateInput()
    {
        var input = Initial();
        var snapshot = (input.Score, input.Confidence, input.SrsIntervalDays, input.EaseFactor, input.LastSeenAt);

        _ = MasteryScoring.Apply(input, outcome: 0.7, asOf: T0.AddHours(1));

        Assert.Equal(snapshot, (input.Score, input.Confidence, input.SrsIntervalDays, input.EaseFactor, input.LastSeenAt));
    }

    [Fact]
    public void Apply_RejectsOutOfRangeOutcome()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MasteryScoring.Apply(Initial(), -0.01, T0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MasteryScoring.Apply(Initial(),  1.01, T0));
    }
}
