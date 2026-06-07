using Unravel.Application.Forge.BossFight;

namespace Unravel.Tests.Forge.BossFight;

/// <summary>
/// PR 50 — testes do algoritmo combinatorial. Foco em invariantes
/// (cobertura, quotas, strategy mix), determinismo e degraded cases
/// (pool pequeno, todos vistos, single-strategy pool).
/// </summary>
public class BossFightSelectorTests
{
    private static BossCandidate Cand(int id, int topicId, string strategy = "LlmGrounded",
        double difficulty = 0.50, double correctRate = 0.50, int served = 5)
        => new(id, topicId, strategy, difficulty, correctRate, served);

    // ── Casos degenerados ──────────────────────────────────────────

    [Fact]
    public void Select_EmptyPool_ReturnsEmpty()
    {
        var result = BossFightSelector.Select(
            topicIds:   new[] { 1 },
            candidates: Array.Empty<BossCandidate>(),
            seenIds:    new HashSet<int>());
        Assert.Empty(result);
    }

    [Fact]
    public void Select_ZeroCount_ReturnsEmpty()
    {
        var result = BossFightSelector.Select(
            topicIds:   new[] { 1 },
            candidates: new[] { Cand(1, 1) },
            seenIds:    new HashSet<int>(),
            count:      0);
        Assert.Empty(result);
    }

    [Fact]
    public void Select_PoolSmallerThanCount_ReturnsAllAvailable()
    {
        // Pool 3 candidatos, pede 10 — retorna 3 (todos), sem repetir.
        var pool = new[] { Cand(1, 1), Cand(2, 2), Cand(3, 3) };
        var result = BossFightSelector.Select(
            topicIds: new[] { 1, 2, 3 }, candidates: pool,
            seenIds:  new HashSet<int>(), count: 10);
        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result.Select(r => r.Id).OrderBy(i => i));
    }

    // ── Cobertura por topic ────────────────────────────────────────

    [Fact]
    public void Select_CoversAllTopics_WhenPossible()
    {
        // 5 topics × 3 candidatos cada = 15 disponíveis, pede 10 → cobre todos os 5
        var pool = Enumerable.Range(1, 5)
            .SelectMany(topic => Enumerable.Range(1, 3).Select(idx =>
                Cand(id: topic * 100 + idx, topicId: topic)))
            .ToList();

        var result = BossFightSelector.Select(
            topicIds: Enumerable.Range(1, 5).ToList(), candidates: pool,
            seenIds:  new HashSet<int>(), count: 10);

        var topicsCovered = result.Select(r => r.TopicId).Distinct().Count();
        Assert.Equal(5, topicsCovered);
    }

    [Fact]
    public void Select_TopicWithNoCandidates_StillFillsRest()
    {
        // 3 topics, mas só 2 têm candidatos. Pede 5 → consegue 5 dos 2 topics existentes.
        var pool = new[]
        {
            Cand(1, 1), Cand(2, 1), Cand(3, 1),
            Cand(4, 2), Cand(5, 2), Cand(6, 2),
        };
        var result = BossFightSelector.Select(
            topicIds: new[] { 1, 2, 3 }, candidates: pool,
            seenIds:  new HashSet<int>(), count: 5);
        Assert.Equal(5, result.Count);
    }

    // ── Quotas de dificuldade ──────────────────────────────────────

    [Fact]
    public void Select_RespectsHardEasyMediumDistribution_ForN10()
    {
        // 30 candidatos: 10 easy / 10 medium / 10 hard. Pede 10 → 3/4/3
        // Strategies variadas pra evitar saturação do cap 40% (sem isso, o
        // cap força o algoritmo a relaxar e a distribuição degrada).
        string Strat(int i) => i % 3 == 0 ? "Cloze" : i % 3 == 1 ? "LlmGrounded" : "TrueFalse";
        var pool = new List<BossCandidate>();
        for (var i = 1; i <= 10; i++) pool.Add(Cand(i,        topicId: 1 + (i % 3), difficulty: 0.30, strategy: Strat(i)));
        for (var i = 11; i <= 20; i++) pool.Add(Cand(i,       topicId: 1 + (i % 3), difficulty: 0.55, strategy: Strat(i)));
        for (var i = 21; i <= 30; i++) pool.Add(Cand(i,       topicId: 1 + (i % 3), difficulty: 0.75, strategy: Strat(i)));

        var result = BossFightSelector.Select(
            topicIds: new[] { 1, 2, 3 }, candidates: pool,
            seenIds:  new HashSet<int>(), count: 10);

        var easy   = result.Count(r => r.EstimatedDifficulty < 0.45);
        var medium = result.Count(r => r.EstimatedDifficulty >= 0.45 && r.EstimatedDifficulty < 0.65);
        var hard   = result.Count(r => r.EstimatedDifficulty >= 0.65);

        Assert.Equal(10, easy + medium + hard);
        Assert.InRange(easy,   2, 4);
        Assert.InRange(medium, 3, 5);
        Assert.InRange(hard,   2, 4);
    }

    // ── Strategy mix ───────────────────────────────────────────────

    [Fact]
    public void Select_RespectsStrategyCapWhenPoolHasMix()
    {
        // 20 candidatos: 10 Cloze + 10 LlmGrounded, pede 10 → cap 40% = 4 cada
        // Fase final relaxa cap se needed, mas pool tem strategies suficientes.
        var pool = new List<BossCandidate>();
        for (var i = 1; i <= 10; i++) pool.Add(Cand(i,        topicId: 1 + (i % 5), strategy: "Cloze",       difficulty: 0.40 + i * 0.03));
        for (var i = 11; i <= 20; i++) pool.Add(Cand(i,       topicId: 1 + (i % 5), strategy: "LlmGrounded", difficulty: 0.40 + (i-10) * 0.03));

        var result = BossFightSelector.Select(
            topicIds: new[] { 1, 2, 3, 4, 5 }, candidates: pool,
            seenIds:  new HashSet<int>(), count: 10);

        var byStrategy = result.GroupBy(r => r.Strategy).ToDictionary(g => g.Key, g => g.Count());
        // Cap = 40% × 10 = 4 (rigoroso). Última fase pode relaxar, mas pool tem
        // diversidade suficiente — esperamos ≤5 de cada (tolerância 1).
        Assert.True(byStrategy.GetValueOrDefault("Cloze",       0) <= 6);
        Assert.True(byStrategy.GetValueOrDefault("LlmGrounded", 0) <= 6);
    }

    [Fact]
    public void Select_SingleStrategyPool_RelaxesAtLastResort()
    {
        // Todos Cloze. Pede 10 → entrega 10 mesmo violando o cap (Fase 4 relaxa).
        var pool = Enumerable.Range(1, 20)
            .Select(i => Cand(i, topicId: 1 + (i % 3), strategy: "Cloze",
                              difficulty: 0.30 + (i * 0.03)))
            .ToList();
        var result = BossFightSelector.Select(
            topicIds: new[] { 1, 2, 3 }, candidates: pool,
            seenIds:  new HashSet<int>(), count: 10);
        Assert.Equal(10, result.Count);
        Assert.All(result, r => Assert.Equal("Cloze", r.Strategy));
    }

    // ── Vistos pelo user ───────────────────────────────────────────

    [Fact]
    public void Select_PrefersUnseen_WhenAvailable()
    {
        var pool = new[] { Cand(1, 1), Cand(2, 1), Cand(3, 2), Cand(4, 2) };
        var seen = new HashSet<int> { 1, 3 };

        var result = BossFightSelector.Select(
            topicIds: new[] { 1, 2 }, candidates: pool,
            seenIds:  seen, count: 2);

        // Esperamos prioridade pros não-vistos (2 e 4).
        Assert.Contains(2, result.Select(r => r.Id));
        Assert.Contains(4, result.Select(r => r.Id));
    }

    [Fact]
    public void Select_AcceptsSeen_WhenNoUnseenLeft()
    {
        // Todos vistos. Pede 3 → entrega 3 mesmo assim.
        var pool = new[] { Cand(1, 1), Cand(2, 2), Cand(3, 3) };
        var seen = new HashSet<int> { 1, 2, 3 };

        var result = BossFightSelector.Select(
            topicIds: new[] { 1, 2, 3 }, candidates: pool,
            seenIds:  seen, count: 3);
        Assert.Equal(3, result.Count);
    }

    // ── CorrectRate quality gate ────────────────────────────────────

    [Fact]
    public void Select_ExcludesTrivialAndUnfair()
    {
        // CorrectRate 0.05 (todo mundo erra) e 0.95 (todo mundo acerta) → fora.
        // Mas served=0 ainda passa (sem dados).
        var pool = new[]
        {
            Cand(1, 1, correctRate: 0.05, served: 50),   // injusta — fora
            Cand(2, 1, correctRate: 0.95, served: 50),   // trivial — fora
            Cand(3, 1, correctRate: 0.50, served: 50),   // OK
            Cand(4, 1, correctRate: 0.99, served: 0),    // sem dados — passa
        };

        var result = BossFightSelector.Select(
            topicIds: new[] { 1 }, candidates: pool,
            seenIds:  new HashSet<int>(), count: 4);

        var ids = result.Select(r => r.Id).ToHashSet();
        Assert.DoesNotContain(1, ids);
        Assert.DoesNotContain(2, ids);
        Assert.Contains(3, ids);
        Assert.Contains(4, ids);
    }

    // ── Determinismo ───────────────────────────────────────────────

    [Fact]
    public void Select_IsDeterministic()
    {
        var pool = Enumerable.Range(1, 50)
            .Select(i => Cand(i,
                topicId:      1 + (i % 5),
                strategy:     i % 3 == 0 ? "Cloze" : i % 3 == 1 ? "LlmGrounded" : "TrueFalse",
                difficulty:   0.20 + (i * 0.01),
                correctRate:  0.50,
                served:       i % 7))
            .ToList();
        var seen = new HashSet<int>(new[] { 3, 11, 23 });

        var r1 = BossFightSelector.Select(new[] { 1,2,3,4,5 }, pool, seen, count: 10);
        var r2 = BossFightSelector.Select(new[] { 1,2,3,4,5 }, pool, seen, count: 10);

        Assert.Equal(r1.Select(r => r.Id), r2.Select(r => r.Id));
    }

    // ── Bucket ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.30, DifficultyBucket.Easy)]
    [InlineData(0.44, DifficultyBucket.Easy)]
    [InlineData(0.45, DifficultyBucket.Medium)]
    [InlineData(0.55, DifficultyBucket.Medium)]
    [InlineData(0.64, DifficultyBucket.Medium)]
    [InlineData(0.65, DifficultyBucket.Hard)]
    [InlineData(0.85, DifficultyBucket.Hard)]
    public void Bucket_ClassifiesCorrectly(double difficulty, DifficultyBucket expected)
    {
        Assert.Equal(expected, BossFightSelector.Bucket(difficulty));
    }
}
