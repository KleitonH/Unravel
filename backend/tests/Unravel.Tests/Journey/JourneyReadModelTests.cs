using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Journey;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Journey;

/// <summary>
/// PR 61 — cobre as leituras do indicador de meta do dia no
/// <see cref="JourneyReadModel"/>: contagem de desafios respondidos hoje
/// (por trilha, via UserSeenChallenge ↔ GeneratedChallenge) e leitura da
/// meta efetiva (com penalidade) do snapshot do cron.
/// </summary>
public class JourneyReadModelTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly JourneyReadModel _sut;
    private static readonly Guid User  = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly DateTime Today = new(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);

    public JourneyReadModelTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new JourneyReadModel(_db);

        // Duas trilhas, perguntas geradas em cada.
        _db.GeneratedChallenge.AddRange(
            Gc(101, trailId: 1), Gc(102, trailId: 1), Gc(103, trailId: 1),
            Gc(201, trailId: 2));
        _db.SaveChanges();
    }

    private static GeneratedChallenge Gc(int id, int trailId) => new()
    {
        Id = id, ContentId = 10, TopicId = 10, TrailId = trailId,
        Strategy = ForgeStrategy.LlmGrounded, Prompt = "p",
        BodyJson = "{}", IsActive = true,
    };

    private void Seen(Guid user, int gcId, DateTime at) =>
        _db.UserSeenChallenge.Add(new UserSeenChallenge
        { UserId = user, GeneratedChallengeId = gcId, SeenAt = at, WasCorrect = true });

    [Fact]
    public async Task CountAnswered_CountsOnlyUserTrailAndWindow()
    {
        Seen(User,  101, Today.AddHours(9));   // conta
        Seen(User,  102, Today.AddHours(10));  // conta
        Seen(User,  103, Today.AddDays(-1));   // ontem → fora da janela
        Seen(User,  201, Today.AddHours(11));  // outra trilha → não conta
        Seen(Other, 101, Today.AddHours(9));   // outro usuário → não conta
        await _db.SaveChangesAsync();

        var n = await _sut.CountChallengesAnsweredAsync(
            User, trailId: 1, Today, Today.AddDays(1));

        Assert.Equal(2, n);
    }

    [Fact]
    public async Task CountAnswered_ZeroWhenNothingToday()
    {
        Seen(User, 101, Today.AddDays(-2));
        await _db.SaveChangesAsync();

        var n = await _sut.CountChallengesAnsweredAsync(
            User, trailId: 1, Today, Today.AddDays(1));

        Assert.Equal(0, n);
    }

    [Fact]
    public async Task GetTodayGoal_ReturnsSnapshotMetaAndPenalty()
    {
        _db.JourneySnapshot.Add(new JourneySnapshot
        {
            UserId = User, TrailId = 1, PlanDate = Today,
            MetaDia = 4, ExtraChallengesPenalty = 1,
            PlanJson = "{}", GeneratedAt = Today,
        });
        await _db.SaveChangesAsync();

        var goal = await _sut.GetTodayGoalAsync(User, trailId: 1, Today);

        Assert.NotNull(goal);
        Assert.Equal(4, goal!.MetaDia);
        Assert.Equal(1, goal.Penalty);
    }

    [Fact]
    public async Task GetTodayGoal_NullWhenNoSnapshotForToday()
    {
        var goal = await _sut.GetTodayGoalAsync(User, trailId: 1, Today);
        Assert.Null(goal);
    }

    public void Dispose() => _db.Dispose();
}
