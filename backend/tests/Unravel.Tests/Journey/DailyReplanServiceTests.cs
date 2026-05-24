using Microsoft.Extensions.Logging.Abstractions;
using Unravel.Application.Journey;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;

namespace Unravel.Tests.Journey;

public class DailyReplanServiceTests
{
    private static readonly DateTime Today     = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Yesterday = Today.AddDays(-1);
    private static readonly Guid     UserId    = Guid.NewGuid();

    private static Topic T(int id) => new(id, contentId: id, trailId: 1, slug: $"t{id}",
        keywords: Array.Empty<Keyword>(), difficultyScore: 0.3, originalOrder: id);

    private static KnowledgeGraph SmallGraph() =>
        new(1, new[] { T(1), T(2), T(3) }, Array.Empty<PrerequisiteEdge>());

    // ── Fakes ────────────────────────────────────────────────────────

    private sealed class FakeReadModel : IDailyReplanReadModel
    {
        public List<ReplanTarget> Targets { get; } = new();
        public Dictionary<Guid, UserCronSnapshot> UserSnapshots { get; } = new();
        public Dictionary<Guid, int> SubmittedYesterday { get; } = new();
        public List<(Guid UserId, int Streak)> StreakUpdates { get; } = new();

        public Task<IReadOnlyList<ReplanTarget>> GetActiveTargetsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ReplanTarget>>(Targets);

        public Task<int> CountUserChallengesSubmittedAsync(
            Guid userId, DateTime _, DateTime __, CancellationToken ct = default)
            => Task.FromResult(SubmittedYesterday.GetValueOrDefault(userId));

        public Task<UserCronSnapshot?> GetUserCronSnapshotAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(UserSnapshots.GetValueOrDefault(userId));

        public Task UpdateUserStreakAsync(Guid userId, int newStreak, CancellationToken ct = default)
        {
            StreakUpdates.Add((userId, newStreak));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSnapshots : IJourneySnapshotRepository
    {
        public List<JourneySnapshot> Saved { get; } = new();
        public List<JourneySnapshot> Seed  { get; } = new();
        public List<(Guid User, int Trail, DateTime Date, bool Met)> GoalMarks { get; } = new();

        public Task<JourneySnapshot?> GetByUserTrailDateAsync(
            Guid userId, int trailId, DateTime planDate, CancellationToken ct = default)
            => Task.FromResult(Seed.FirstOrDefault(s =>
                s.UserId == userId && s.TrailId == trailId && s.PlanDate == planDate));

        public Task UpsertAsync(JourneySnapshot snapshot, CancellationToken ct = default)
        {
            Saved.RemoveAll(s => s.UserId == snapshot.UserId && s.TrailId == snapshot.TrailId
                                  && s.PlanDate == snapshot.PlanDate);
            Saved.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task MarkGoalAsync(Guid userId, int trailId, DateTime planDate, bool met, CancellationToken ct = default)
        {
            GoalMarks.Add((userId, trailId, planDate, met));
            var s = Seed.FirstOrDefault(x => x.UserId == userId && x.TrailId == trailId && x.PlanDate == planDate);
            if (s is not null) s.MetGoal = met;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGraphCache : IKnowledgeGraphCache
    {
        private readonly KnowledgeGraph _graph;
        public FakeGraphCache(KnowledgeGraph g) => _graph = g;
        public Task<KnowledgeGraph> GetOrBuildAsync(int trailId, CancellationToken ct = default) => Task.FromResult(_graph);
        public void Invalidate(int _) { }
    }

    private sealed class FakeMasteryRepo : IMasteryRepository
    {
        public Task<Mastery?> GetAsync(Guid u, int t, CancellationToken ct = default) => Task.FromResult<Mastery?>(null);
        public Task<IReadOnlyList<Mastery>> GetByTrailAsync(Guid u, int t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Mastery>>(Array.Empty<Mastery>());
        public Task<IReadOnlyList<Mastery>> GetDueForReviewAsync(Guid u, int t, DateTime asOf, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Mastery>>(Array.Empty<Mastery>());
        public Task UpsertAsync(Mastery m, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpsertManyAsync(IEnumerable<Mastery> m, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeEventBus : IJourneyEventBus
    {
        public List<JourneyEvent> Published { get; } = new();
        public Task PublishAsync(JourneyEvent evt, CancellationToken ct = default)
        {
            Published.Add(evt);
            return Task.CompletedTask;
        }
    }

    private DailyReplanService Create(
        FakeReadModel rm, IJourneySnapshotRepository snaps, FakeEventBus bus, KnowledgeGraph? graph = null)
    {
        return new DailyReplanService(
            rm, snaps,
            new FakeGraphCache(graph ?? SmallGraph()),
            new FakeMasteryRepo(),
            new JourneyPlanner(),
            bus,
            NullLogger<DailyReplanService>.Instance);
    }

    // ── Casos ────────────────────────────────────────────────────────

    [Fact]
    public async Task Run_NoTargets_ReturnsZeroReport()
    {
        var sut = Create(new FakeReadModel(), new FakeSnapshots(), new FakeEventBus());
        var report = await sut.RunAsync(Today);

        Assert.Equal(0, report.Processed);
        Assert.Equal(0, report.Failures);
        Assert.Equal(0, report.YesterdayGoalMet);
    }

    [Fact]
    public async Task Run_PersistsSnapshotForEachTarget_AndPublishesEvent()
    {
        var rm    = new FakeReadModel();
        var snaps = new FakeSnapshots();
        var bus   = new FakeEventBus();
        rm.Targets.Add(new ReplanTarget(UserId, 1));
        rm.UserSnapshots[UserId] = new UserCronSnapshot(Lives: 5, StreakDays: 3, LastActivityDate: Today);

        var sut = Create(rm, snaps, bus);
        var report = await sut.RunAsync(Today);

        Assert.Equal(1, report.Processed);
        Assert.Equal(1, snaps.Saved.Count);
        Assert.Equal(Today, snaps.Saved[0].PlanDate);
        Assert.Contains(bus.Published, e => e is DailyPlanGenerated);
    }

    [Fact]
    public async Task Run_YesterdayGoalMet_NoPenalty_AndMarksSnapshot()
    {
        var rm    = new FakeReadModel();
        var snaps = new FakeSnapshots();
        var bus   = new FakeEventBus();
        rm.Targets.Add(new ReplanTarget(UserId, 1));
        rm.UserSnapshots[UserId] = new UserCronSnapshot(Lives: 5, StreakDays: 3, LastActivityDate: Today);
        rm.SubmittedYesterday[UserId] = 4;  // meta era 3 → cumpriu

        snaps.Seed.Add(new JourneySnapshot
        {
            UserId = UserId, TrailId = 1, PlanDate = Yesterday,
            MetaDia = 3, ExtraChallengesPenalty = 0,
            PlanJson = "{}", MetGoal = null,
        });

        var sut = Create(rm, snaps, bus);
        var report = await sut.RunAsync(Today);

        Assert.Equal(1, report.YesterdayGoalMet);
        var markedTrue = snaps.GoalMarks.Single();
        Assert.True(markedTrue.Met);
        Assert.Equal(0, snaps.Saved[0].ExtraChallengesPenalty);
    }

    [Fact]
    public async Task Run_YesterdayGoalMissed_AddsPenaltyToTodayMeta()
    {
        var rm    = new FakeReadModel();
        var snaps = new FakeSnapshots();
        var bus   = new FakeEventBus();
        rm.Targets.Add(new ReplanTarget(UserId, 1));
        rm.UserSnapshots[UserId] = new UserCronSnapshot(Lives: 5, StreakDays: 3, LastActivityDate: Today);
        rm.SubmittedYesterday[UserId] = 1;  // meta era 3 → não cumpriu

        snaps.Seed.Add(new JourneySnapshot
        {
            UserId = UserId, TrailId = 1, PlanDate = Yesterday,
            MetaDia = 3, ExtraChallengesPenalty = 0,
            PlanJson = "{}", MetGoal = null,
        });

        var sut = Create(rm, snaps, bus);
        await sut.RunAsync(Today);

        Assert.False(snaps.GoalMarks.Single().Met);
        Assert.Equal(1, snaps.Saved[0].ExtraChallengesPenalty);
        var ev = (DailyPlanGenerated)bus.Published.OfType<DailyPlanGenerated>().Single();
        Assert.Equal(1, ev.ExtraPenalty);
        Assert.False(ev.MetGoalYesterday);
    }

    [Fact]
    public async Task Run_StreakResetWhenInactiveTwoOrMoreDays()
    {
        var rm    = new FakeReadModel();
        var snaps = new FakeSnapshots();
        var bus   = new FakeEventBus();
        rm.Targets.Add(new ReplanTarget(UserId, 1));
        rm.UserSnapshots[UserId] = new UserCronSnapshot(
            Lives: 5, StreakDays: 12, LastActivityDate: Today.AddDays(-3));

        var sut = Create(rm, snaps, bus);
        await sut.RunAsync(Today);

        Assert.Single(rm.StreakUpdates);
        Assert.Equal(0, rm.StreakUpdates[0].Streak);
        Assert.Contains(bus.Published, e => e is StreakReset);
    }

    [Fact]
    public async Task Run_StreakIntact_WhenActiveYesterday()
    {
        var rm    = new FakeReadModel();
        var snaps = new FakeSnapshots();
        var bus   = new FakeEventBus();
        rm.Targets.Add(new ReplanTarget(UserId, 1));
        rm.UserSnapshots[UserId] = new UserCronSnapshot(
            Lives: 5, StreakDays: 12, LastActivityDate: Yesterday);

        var sut = Create(rm, snaps, bus);
        await sut.RunAsync(Today);

        Assert.Empty(rm.StreakUpdates);
        Assert.DoesNotContain(bus.Published, e => e is StreakReset);
    }

    [Fact]
    public async Task Run_ContinueOnFailure_DoesNotBlockOtherTargets()
    {
        var rm    = new FakeReadModel();
        var snaps = new BrokenSnapshotsForOne(failOnTrailId: 1);
        var bus   = new FakeEventBus();
        rm.Targets.Add(new ReplanTarget(UserId, 1));
        rm.Targets.Add(new ReplanTarget(UserId, 2));
        rm.UserSnapshots[UserId] = new UserCronSnapshot(Lives: 5, StreakDays: 0, LastActivityDate: Today);

        var sut = Create(rm, snaps, bus);
        var report = await sut.RunAsync(Today);

        Assert.Equal(1, report.Processed);
        Assert.Equal(1, report.Failures);
        Assert.Single(snaps.Saved.Where(s => s.TrailId == 2));
    }

    private sealed class BrokenSnapshotsForOne : IJourneySnapshotRepository
    {
        private readonly int _failOnTrailId;
        public List<JourneySnapshot> Saved { get; } = new();

        public BrokenSnapshotsForOne(int failOnTrailId) => _failOnTrailId = failOnTrailId;

        public Task<JourneySnapshot?> GetByUserTrailDateAsync(Guid u, int t, DateTime d, CancellationToken ct = default)
            => Task.FromResult<JourneySnapshot?>(null);

        public Task UpsertAsync(JourneySnapshot snap, CancellationToken ct = default)
        {
            if (snap.TrailId == _failOnTrailId)
                throw new InvalidOperationException("BOOM");
            Saved.Add(snap);
            return Task.CompletedTask;
        }

        public Task MarkGoalAsync(Guid u, int t, DateTime d, bool m, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
