using Unravel.Application.Journey;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;

namespace Unravel.Tests.Journey;

public class JourneyPlannerTests
{
    private static readonly DateTime T0 = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);
    private readonly JourneyPlanner _sut = new();

    private static Topic T(int id, int order, double difficulty = 0.3) =>
        new(id, contentId: id, trailId: 1, slug: $"t{id}",
            keywords: Array.Empty<Keyword>(),
            difficultyScore: difficulty,
            originalOrder: order);

    // ── Cold start ────────────────────────────────────────────────────

    [Fact]
    public void Plan_ColdStart_ReturnsOnlyRootsOfTheDag()
    {
        // 1 → 2 → 3 ; user sem nenhuma mastery
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1), T(3,2) },
            edges:  new[] { new PrerequisiteEdge(1,2,0.5), new PrerequisiteEdge(2,3,0.5) });

        var plan = _sut.Plan(new JourneyPlanInput(
            UserId: Guid.NewGuid(), Graph: graph, Masteries: Array.Empty<Mastery>(),
            LivesAvailable: 5, StreakDays: 0, AsOf: T0));

        Assert.NotEmpty(plan.Today);
        Assert.Equal(1, plan.Today[0].TopicId);
        Assert.All(plan.Today, item => Assert.Equal(JourneyReason.NewLearning, item.Reason));
        // Tópico 2 está bloqueado por 1; só pode aparecer em upcoming depois que 1 for dominado.
        // Como cold start: today só tem raízes (1).
        Assert.DoesNotContain(plan.Today, i => i.TopicId == 2);
        Assert.DoesNotContain(plan.Today, i => i.TopicId == 3);
    }

    // ── Gating de pré-requisitos ─────────────────────────────────────

    [Fact]
    public void Plan_GatesByPrerequisites_OnlyAdmitsWhenPrereqIsMastered()
    {
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1) },
            edges:  new[] { new PrerequisiteEdge(1, 2, 0.5) });

        var userId = Guid.NewGuid();
        var masteries = new[]
        {
            // Topic 1 dominado, recente, sem revisão vencida
            new Mastery { UserId = userId, TopicId = 1, TrailId = 1,
                          Score = 0.95, Confidence = 5, LastSeenAt = T0.AddDays(-1),
                          SrsIntervalDays = 7, EaseFactor = 2.5 }
        };

        var plan = _sut.Plan(new JourneyPlanInput(
            userId, graph, masteries, LivesAvailable: 5, StreakDays: 0, AsOf: T0));

        Assert.Contains(plan.Today, i => i.TopicId == 2);
    }

    [Fact]
    public void Plan_DoesNotAdmitWhenPrereqEffectiveMasteryBelowThreshold()
    {
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1) },
            edges:  new[] { new PrerequisiteEdge(1, 2, 0.5) });

        var userId = Guid.NewGuid();
        var masteries = new[]
        {
            // score era 0.95 há 60 dias → decaiu para < 0.05 com meia-vida 14d
            new Mastery { UserId = userId, TopicId = 1, TrailId = 1,
                          Score = 0.95, Confidence = 5, LastSeenAt = T0.AddDays(-60),
                          SrsIntervalDays = 7, EaseFactor = 2.5 }
        };

        var plan = _sut.Plan(new JourneyPlanInput(
            userId, graph, masteries, LivesAvailable: 5, StreakDays: 0, AsOf: T0));

        // Topic 2 deve estar bloqueado (prereq 1 esquecido); topic 1 entra para revisão.
        Assert.DoesNotContain(plan.Today.Concat(plan.Upcoming), i => i.TopicId == 2);
        Assert.Contains(plan.Today, i => i.TopicId == 1);
    }

    // ── SRS / revisão vencida ────────────────────────────────────────

    [Fact]
    public void Plan_DueReviewItems_TaggedAsDueReview_AndPrioritizedOverReinforce()
    {
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1) },
            edges:  Array.Empty<PrerequisiteEdge>());

        var userId = Guid.NewGuid();
        var masteries = new[]
        {
            // Topic 1: revisão vencida há 5 dias
            new Mastery { UserId = userId, TopicId = 1, TrailId = 1,
                          Score = 0.6, Confidence = 3, LastSeenAt = T0.AddDays(-12),
                          SrsIntervalDays = 7, EaseFactor = 2.0 },
            // Topic 2: visto ontem, ainda não dominado, sem revisão vencida → Reinforce
            new Mastery { UserId = userId, TopicId = 2, TrailId = 1,
                          Score = 0.4, Confidence = 2, LastSeenAt = T0.AddDays(-1),
                          SrsIntervalDays = 7, EaseFactor = 2.0 },
        };

        var plan = _sut.Plan(new JourneyPlanInput(
            userId, graph, masteries, LivesAvailable: 5, StreakDays: 0, AsOf: T0));

        var t1 = plan.Today.Concat(plan.Upcoming).First(i => i.TopicId == 1);
        var t2 = plan.Today.Concat(plan.Upcoming).First(i => i.TopicId == 2);

        Assert.Equal(JourneyReason.DueReview, t1.Reason);
        Assert.Equal(JourneyReason.Reinforce, t2.Reason);
        Assert.True(t1.Priority > t2.Priority,
            $"DueReview ({t1.Priority:F3}) should outrank Reinforce ({t2.Priority:F3})");
    }

    // ── Tópicos dominados ────────────────────────────────────────────

    [Fact]
    public void Plan_ExcludesMasteredTopicsWithoutDueReview()
    {
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1) },
            edges:  Array.Empty<PrerequisiteEdge>());

        var userId = Guid.NewGuid();
        var masteries = new[]
        {
            // Dominado, revisão daqui a 30 dias
            new Mastery { UserId = userId, TopicId = 1, TrailId = 1,
                          Score = 0.95, Confidence = 10, LastSeenAt = T0,
                          SrsIntervalDays = 30, EaseFactor = 2.5 }
        };

        var plan = _sut.Plan(new JourneyPlanInput(
            userId, graph, masteries, LivesAvailable: 5, StreakDays: 0, AsOf: T0));

        Assert.DoesNotContain(plan.Today.Concat(plan.Upcoming), i => i.TopicId == 1);
        Assert.Contains(plan.Today, i => i.TopicId == 2);
    }

    // ── metaDia ──────────────────────────────────────────────────────

    [Fact]
    public void Plan_MetaDia_RespectsLivesCap()
    {
        var graph = new KnowledgeGraph(1,
            topics: Enumerable.Range(1, 10).Select(i => T(i, i)).ToArray(),
            edges:  Array.Empty<PrerequisiteEdge>());

        var plan = _sut.Plan(new JourneyPlanInput(
            Guid.NewGuid(), graph, Array.Empty<Mastery>(),
            LivesAvailable: 2, StreakDays: 0, AsOf: T0));

        // 2 vidas * 1.5 = 3 → meta capada em 3
        Assert.Equal(3, plan.MetaDia);
        Assert.Equal(3, plan.Today.Count);
    }

    [Fact]
    public void Plan_MetaDia_RespectsStreakCap()
    {
        var graph = new KnowledgeGraph(1,
            topics: Enumerable.Range(1, 20).Select(i => T(i, i)).ToArray(),
            edges:  Array.Empty<PrerequisiteEdge>());

        // streak grande, vidas folgadas — cap pelo streak (3 + 30/7 = 7)
        var plan = _sut.Plan(new JourneyPlanInput(
            Guid.NewGuid(), graph, Array.Empty<Mastery>(),
            LivesAvailable: 10, StreakDays: 30, AsOf: T0));

        Assert.InRange(plan.MetaDia, 7, 8);
    }

    [Fact]
    public void Plan_MetaDia_CappedByCandidatesAvailable()
    {
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1) },
            edges:  Array.Empty<PrerequisiteEdge>());

        var plan = _sut.Plan(new JourneyPlanInput(
            Guid.NewGuid(), graph, Array.Empty<Mastery>(),
            LivesAvailable: 10, StreakDays: 30, AsOf: T0));

        // Cap teórico 7, mas só 2 candidatos.
        Assert.Equal(2, plan.MetaDia);
        Assert.Equal(2, plan.Today.Count);
        Assert.Empty(plan.Upcoming);
    }

    [Fact]
    public void Plan_ZeroLives_StillReturnsOneItem()
    {
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1) },
            edges:  Array.Empty<PrerequisiteEdge>());

        var plan = _sut.Plan(new JourneyPlanInput(
            Guid.NewGuid(), graph, Array.Empty<Mastery>(),
            LivesAvailable: 0, StreakDays: 0, AsOf: T0));

        // MinMetaDia=1 garante que o user nunca fica sem nada para fazer.
        Assert.Equal(1, plan.MetaDia);
        Assert.Single(plan.Today);
    }

    // ── Determinismo ─────────────────────────────────────────────────

    [Fact]
    public void Plan_IsFullyDeterministic()
    {
        var graph = new KnowledgeGraph(1,
            topics: Enumerable.Range(1, 6).Select(i => T(i, i, difficulty: 0.1 * i)).ToArray(),
            edges:  new[]
            {
                new PrerequisiteEdge(1,2,0.5), new PrerequisiteEdge(2,3,0.5),
                new PrerequisiteEdge(3,4,0.5), new PrerequisiteEdge(4,5,0.5),
            });

        var userId = Guid.NewGuid();
        var masteries = new[]
        {
            new Mastery { UserId = userId, TopicId = 1, TrailId = 1,
                          Score = 0.85, Confidence = 4, LastSeenAt = T0.AddDays(-3),
                          SrsIntervalDays = 7, EaseFactor = 2.5 },
        };

        var input = new JourneyPlanInput(userId, graph, masteries,
                                         LivesAvailable: 5, StreakDays: 14, AsOf: T0);

        var a = _sut.Plan(input);
        var b = _sut.Plan(input);

        Assert.Equal(a.MetaDia, b.MetaDia);
        Assert.Equal(a.Today.Select(i => i.TopicId), b.Today.Select(i => i.TopicId));
        Assert.Equal(a.Upcoming.Select(i => i.TopicId), b.Upcoming.Select(i => i.TopicId));
    }

    // ── Vazio ─────────────────────────────────────────────────────────

    [Fact]
    public void Plan_NoCandidates_ReturnsEmptyPlan_WithZeroMetaDia()
    {
        // Todos dominados, sem revisão vencida.
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1) },
            edges:  Array.Empty<PrerequisiteEdge>());

        var userId = Guid.NewGuid();
        var masteries = new[]
        {
            new Mastery { UserId = userId, TopicId = 1, TrailId = 1,
                          Score = 0.95, Confidence = 10, LastSeenAt = T0, SrsIntervalDays = 30, EaseFactor = 2.5 },
            new Mastery { UserId = userId, TopicId = 2, TrailId = 1,
                          Score = 0.95, Confidence = 10, LastSeenAt = T0, SrsIntervalDays = 30, EaseFactor = 2.5 },
        };

        var plan = _sut.Plan(new JourneyPlanInput(userId, graph, masteries,
            LivesAvailable: 5, StreakDays: 0, AsOf: T0));

        Assert.Equal(0, plan.MetaDia);
        Assert.Empty(plan.Today);
        Assert.Empty(plan.Upcoming);
    }
}
