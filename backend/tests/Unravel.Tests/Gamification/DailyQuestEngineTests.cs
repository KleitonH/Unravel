using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Gamification;
using Unravel.Infrastructure.Gamification;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Gamification;

/// <summary>
/// Motor de missões diárias: atribui o conjunto do dia, avança por atividade,
/// e credita novelo + caixinha uma única vez quando cada missão fecha.
/// Fan-out social isolado por fakes. EF InMemory.
/// </summary>
public class DailyQuestEngineTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly FakePartnerships    _partnerships = new();
    private readonly FakeCaixinha        _caixinha     = new();
    private readonly DailyQuestEngine    _sut;
    private readonly Guid _user = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 6, 10, 14, 0, 0, DateTimeKind.Utc);

    public DailyQuestEngineTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new ApplicationDbContext(options);
        _sut = new DailyQuestEngine(_db, _caixinha, _partnerships, NullLogger<DailyQuestEngine>.Instance);
    }

    // Quantas missões do dia casam com um tipo e têm target ≤ N.
    private int Completions(ActivityKind kind, int withCount) =>
        DailyQuestCatalog.ForDate(_now).Count(d => d.Activity == kind && d.Target <= withCount);

    [Fact]
    public async Task GetToday_AssignsRotatingSet_ProgressZero()
    {
        var today = await _sut.GetTodayAsync(_user, _now);

        var expected = DailyQuestCatalog.ForDate(_now).Select(d => d.Key).ToArray();
        Assert.Equal(expected, today.Select(q => q.Key).ToArray());
        Assert.All(today, q => Assert.Equal(0, q.Progress));
        Assert.All(today, q => Assert.False(q.Completed));
        Assert.Equal(DailyQuestCatalog.QuestsPerDay, today.Count);
    }

    [Fact]
    public async Task GetToday_IsIdempotent_NoDuplicateRows()
    {
        await _sut.GetTodayAsync(_user, _now);
        await _sut.GetTodayAsync(_user, _now);

        var rows = await _db.UserDailyQuest.CountAsync(q => q.UserId == _user && q.QuestDate == _now.Date);
        Assert.Equal(DailyQuestCatalog.QuestsPerDay, rows);
    }

    [Fact]
    public async Task Record_QuizAnswered_AdvancesButDoesNotCompleteBelowTarget()
    {
        await _sut.RecordAsync(_user, ActivityKind.QuizAnswered, 1, _now);

        var today = await _sut.GetTodayAsync(_user, _now);
        var answered = today.Where(q => DailyQuestCatalog.Find(q.Key)!.Activity == ActivityKind.QuizAnswered).ToList();
        Assert.All(answered, q => Assert.Equal(1, q.Progress));
        Assert.All(answered, q => Assert.False(q.Completed)); // menor target é 5
        Assert.Equal(0, _partnerships.Calls);
        Assert.Equal(0, _caixinha.Calls);
    }

    [Fact]
    public async Task Record_CompletingQuest_CreditsNoveloAndCaixinhaOnce()
    {
        var expected = Completions(ActivityKind.QuizAnswered, 5); // missões "responder ≤5"
        await _sut.RecordAsync(_user, ActivityKind.QuizAnswered, 5, _now);

        Assert.Equal(expected, _partnerships.Calls);
        Assert.Equal(expected, _caixinha.Calls);
        Assert.Equal(expected * DailyQuestCatalog.CaixinhaPointsPerQuest, _caixinha.Points);
    }

    [Fact]
    public async Task Record_AlreadyCompleted_DoesNotCreditAgain()
    {
        await _sut.RecordAsync(_user, ActivityKind.QuizAnswered, 100, _now); // completa todas de "responder"
        var afterFirst = _partnerships.Calls;
        Assert.Equal(Completions(ActivityKind.QuizAnswered, 100), afterFirst);

        await _sut.RecordAsync(_user, ActivityKind.QuizAnswered, 100, _now); // nada novo a completar
        Assert.Equal(afterFirst, _partnerships.Calls);
    }

    [Fact]
    public async Task Record_QuizCorrect_OnlyAdvancesCorrectQuests()
    {
        var expected = Completions(ActivityKind.QuizCorrect, 100);
        await _sut.RecordAsync(_user, ActivityKind.QuizCorrect, 100, _now);

        Assert.Equal(expected, _partnerships.Calls);

        // Nenhuma missão de "responder" foi tocada.
        var today = await _sut.GetTodayAsync(_user, _now);
        var answered = today.Where(q => DailyQuestCatalog.Find(q.Key)!.Activity == ActivityKind.QuizAnswered);
        Assert.All(answered, q => Assert.Equal(0, q.Progress));
    }

    [Fact]
    public async Task Record_ProgressClampsAtTarget()
    {
        await _sut.RecordAsync(_user, ActivityKind.QuizAnswered, 999, _now);
        var today = await _sut.GetTodayAsync(_user, _now);
        Assert.All(
            today.Where(q => DailyQuestCatalog.Find(q.Key)!.Activity == ActivityKind.QuizAnswered),
            q => Assert.Equal(DailyQuestCatalog.Find(q.Key)!.Target, q.Progress));
    }

    public void Dispose() => _db.Dispose();

    // ── Fakes ────────────────────────────────────────────────────────
    private sealed class FakeCaixinha : ICaixinhaContributionService
    {
        public int Calls { get; private set; }
        public int Points { get; private set; }
        public Task ContributeAsync(Guid userId, int xpEarned, DateTime now, CancellationToken ct = default)
        {
            Calls++; Points += xpEarned;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePartnerships : IPartnershipService
    {
        public int Calls { get; private set; }
        public Task<AddProgressResult> AddProgressAsync(Guid userId, int amount, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new AddProgressResult(1, false, false));
        }

        public Task<PartnershipActionResult> RequestAsync(Guid a, Guid b, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PartnershipActionResult> RespondAsync(Guid u, int id, bool ok, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PartnershipActionResult> BreakAsync(Guid u, int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PartnershipDto>> GetMineAsync(Guid u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PartnershipRequestsDto> GetRequestsAsync(Guid u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> EvaluateInactivityAsync(DateTime now, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
