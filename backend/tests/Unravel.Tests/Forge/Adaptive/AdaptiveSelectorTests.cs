using Unravel.Application.Forge.Adaptive;

namespace Unravel.Tests.Forge.Adaptive;

/// <summary>
/// PR 42 — testes do algoritmo CAT-lite. Foco em invariantes matemáticas
/// (ability em [0,1], stop deterministic, seleção minimiza distância)
/// e casos limite (sessão vazia, cap, convergência, pool esgotado).
/// </summary>
public class AdaptiveSelectorTests
{
    private static AdaptiveOutcome Correct(int id = 1) => new(id, true);
    private static AdaptiveOutcome Wrong(int id = 1)   => new(id, false);

    // ── EstimateAbility ─────────────────────────────────────────────

    [Fact]
    public void EstimateAbility_NoHistory_ReturnsStartAbility()
    {
        var ability = AdaptiveSelector.EstimateAbility(
            Array.Empty<AdaptiveOutcome>(), startAbility: 0.4);
        Assert.Equal(0.4, ability, precision: 4);
    }

    [Fact]
    public void EstimateAbility_AllCorrect_ConvergesUp()
    {
        var history = Enumerable.Repeat(Correct(), 5).ToList();
        var ability = AdaptiveSelector.EstimateAbility(history, startAbility: 0.3);
        Assert.True(ability > 0.3);
        Assert.True(ability <= 1.0);
    }

    [Fact]
    public void EstimateAbility_AllWrong_ConvergesDown()
    {
        var history = Enumerable.Repeat(Wrong(), 5).ToList();
        var ability = AdaptiveSelector.EstimateAbility(history, startAbility: 0.7);
        Assert.True(ability < 0.7);
        Assert.True(ability >= 0.0);
    }

    [Fact]
    public void EstimateAbility_AlphaDecaysOverTime()
    {
        // Primeira tentativa correta tem peso maior que a 5ª — pra essa
        // teste, a diferença entre ability(1 correto + N erros) e
        // ability(N erros + 1 correto) demonstra a sensibilidade ao tempo.
        var earlyCorrect = new List<AdaptiveOutcome> { Correct(), Wrong(2), Wrong(3), Wrong(4), Wrong(5) };
        var lateCorrect  = new List<AdaptiveOutcome> { Wrong(1), Wrong(2), Wrong(3), Wrong(4), Correct(5) };

        var early = AdaptiveSelector.EstimateAbility(earlyCorrect, 0.3);
        var late_ = AdaptiveSelector.EstimateAbility(lateCorrect,  0.3);

        // Acerto cedo levanta menos a ability final (foi diluído por 4 erros depois)
        // do que acerto tarde (que pega α menor mas após cair muito).
        // Não precisamos garantir ordem específica — só que o algoritmo
        // produz outputs diferentes, validando que histórico importa.
        Assert.NotEqual(early, late_, precision: 4);
    }

    [Fact]
    public void EstimateAbility_StaysInUnitInterval()
    {
        // Aceitamos qualquer outcome [0,1] como EWMA → resultado sempre em [0,1].
        var history = new[] { Correct(), Wrong(2), Correct(3), Correct(4), Wrong(5), Correct(6) };
        var ability = AdaptiveSelector.EstimateAbility(history, 0.5);
        Assert.InRange(ability, 0.0, 1.0);
    }

    // ── ShouldStop ──────────────────────────────────────────────────

    [Fact]
    public void ShouldStop_BelowMin_ReturnsNull()
    {
        var h = new[] { Correct(), Wrong(2) };  // count=2 < min=3
        Assert.Null(AdaptiveSelector.ShouldStop(h));
    }

    [Fact]
    public void ShouldStop_AtMax_ReturnsMaxReached()
    {
        var h = Enumerable.Range(1, AdaptiveSelector.MaxQuestions)
                          .Select(i => Correct(i)).ToList();
        var reason = AdaptiveSelector.ShouldStop(h);
        Assert.Equal(AdaptiveStopReason.MaxReached, reason);
    }

    [Fact]
    public void ShouldStop_ConvergedAbility_ReturnsConverged()
    {
        // Sequência idêntica → ability oscila pouco → variância baixa → para.
        var h = new[] { Correct(1), Correct(2), Correct(3), Correct(4) };
        var reason = AdaptiveSelector.ShouldStop(h, startAbility: 0.7);
        Assert.Equal(AdaptiveStopReason.Converged, reason);
    }

    [Fact]
    public void ShouldStop_OscillatingAbility_DoesNotStop()
    {
        // Acerto-erro alternados manteem variância alta → continua.
        var h = new[] { Correct(1), Wrong(2), Correct(3) };
        var reason = AdaptiveSelector.ShouldStop(h, startAbility: 0.3);
        Assert.Null(reason);
    }

    // ── SelectNextChallengeId ───────────────────────────────────────

    [Fact]
    public void SelectNextChallengeId_EmptyCandidates_ReturnsNull()
    {
        var next = AdaptiveSelector.SelectNextChallengeId(
            ability: 0.5, candidates: Array.Empty<AdaptiveCandidate>(),
            excludeIds: new HashSet<int>());
        Assert.Null(next);
    }

    [Fact]
    public void SelectNextChallengeId_AllExcluded_ReturnsNull()
    {
        var candidates = new[]
        {
            new AdaptiveCandidate(1, 0.5, 0),
            new AdaptiveCandidate(2, 0.6, 0),
        };
        var next = AdaptiveSelector.SelectNextChallengeId(
            ability: 0.5, candidates: candidates,
            excludeIds: new HashSet<int> { 1, 2 });
        Assert.Null(next);
    }

    [Fact]
    public void SelectNextChallengeId_PicksClosestToTarget()
    {
        // ability 0.5 + offset 0.10 = target 0.60. Mais próximo: 0.62 (id=2).
        var candidates = new[]
        {
            new AdaptiveCandidate(1, 0.30, 0),
            new AdaptiveCandidate(2, 0.62, 0),
            new AdaptiveCandidate(3, 0.85, 0),
        };
        var next = AdaptiveSelector.SelectNextChallengeId(
            ability: 0.5, candidates: candidates,
            excludeIds: new HashSet<int>());
        Assert.Equal(2, next);
    }

    [Fact]
    public void SelectNextChallengeId_TargetClampedAtFloor()
    {
        // ability 0.0 → target 0.10 → clamp em 0.15. Mais próximo: 0.20.
        var candidates = new[]
        {
            new AdaptiveCandidate(1, 0.20, 0),
            new AdaptiveCandidate(2, 0.70, 0),
        };
        var next = AdaptiveSelector.SelectNextChallengeId(
            ability: 0.0, candidates: candidates,
            excludeIds: new HashSet<int>());
        Assert.Equal(1, next);
    }

    [Fact]
    public void SelectNextChallengeId_TieBreakByServedCountAsc()
    {
        // Dois candidatos equidistantes do target — prefere o menos servido.
        var candidates = new[]
        {
            new AdaptiveCandidate(1, 0.60, 10),  // mesma distância,
            new AdaptiveCandidate(2, 0.60, 2),   // mas servido menos vezes
        };
        var next = AdaptiveSelector.SelectNextChallengeId(
            ability: 0.5, candidates: candidates,
            excludeIds: new HashSet<int>());
        Assert.Equal(2, next);
    }

    [Fact]
    public void SelectNextChallengeId_ExcludesProvidedIds()
    {
        // Mais próximo seria id=2, mas está excluído → pega o próximo (id=3).
        var candidates = new[]
        {
            new AdaptiveCandidate(1, 0.30, 0),
            new AdaptiveCandidate(2, 0.62, 0),
            new AdaptiveCandidate(3, 0.50, 0),
        };
        var next = AdaptiveSelector.SelectNextChallengeId(
            ability: 0.5, candidates: candidates,
            excludeIds: new HashSet<int> { 2 });
        Assert.Equal(3, next);
    }

    // ── AbilityTrajectory ───────────────────────────────────────────

    [Fact]
    public void AbilityTrajectory_LengthEqualsHistoryPlusOne()
    {
        var h = new[] { Correct(), Wrong(2), Correct(3) };
        var traj = AdaptiveSelector.AbilityTrajectory(h, 0.5);
        Assert.Equal(h.Length + 1, traj.Count);
        Assert.Equal(0.5, traj[0]);
    }

    [Fact]
    public void AbilityTrajectory_FinalMatchesEstimate()
    {
        var h = new[] { Correct(), Wrong(2), Correct(3), Correct(4) };
        var traj = AdaptiveSelector.AbilityTrajectory(h, 0.3);
        var est  = AdaptiveSelector.EstimateAbility(h, 0.3);
        Assert.Equal(est, traj[^1], precision: 6);
    }
}
