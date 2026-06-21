using Microsoft.EntityFrameworkCore;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Notifications;
using Unravel.Infrastructure.Persistence;
using Unravel.Infrastructure.Social;

namespace Unravel.Tests.Social;

/// <summary>
/// PR 66 — ligas semanais: criação Bronze + baseline, XP da semana, leaderboard,
/// rollover (promove topo / rebaixa fundo / reseta baseline) e regra de não
/// promover por inatividade. EF InMemory.
/// </summary>
public class LeagueServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly LeagueService _sut;
    private readonly DateTime _w1 = new(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc);  // semana A
    private DateTime W2 => _w1.AddDays(7);                                         // semana B

    public LeagueServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new LeagueService(_db, new NotificationService(_db));
    }

    private User MakeUser(string name, int xp)
    {
        var u = User.Create(name, Email.Create($"{name}@u.test".ToLower()), "h");
        u.Xp = xp;
        _db.User.Add(u);
        _db.SaveChanges();
        return u;
    }

    private static string Week(DateTime d)
    {
        var dd = d.Date;
        var delta = ((int)dd.DayOfWeek + 6) % 7;
        return dd.AddDays(-delta).ToString("yyyy-MM-dd");
    }

    [Fact]
    public async Task FirstAccess_StartsBronze_WeeklyZero()
    {
        var ana = MakeUser("Ana", 500);
        var dto = await _sut.GetMyLeagueAsync(ana.Id, _w1);

        Assert.Equal("Bronze", dto.Tier);
        Assert.Equal(0, dto.WeeklyXp);     // baseline = xp atual
        Assert.Equal(1, dto.Rank);
        Assert.Equal("Prata", dto.NextTier);
        Assert.Null(dto.PrevTier);
    }

    [Fact]
    public async Task WeeklyXp_IsCurrentMinusBaseline()
    {
        var ana = MakeUser("Ana", 500);
        await _sut.GetMyLeagueAsync(ana.Id, _w1); // baseline 500
        ana.Xp = 800; await _db.SaveChangesAsync();

        var dto = await _sut.GetMyLeagueAsync(ana.Id, _w1);
        Assert.Equal(300, dto.WeeklyXp);
    }

    [Fact]
    public async Task Leaderboard_OrdersByWeeklyDesc()
    {
        var ana = MakeUser("Ana", 0);
        var bia = MakeUser("Bia", 0);
        await _sut.GetMyLeagueAsync(ana.Id, _w1);
        await _sut.GetMyLeagueAsync(bia.Id, _w1);
        ana.Xp = 100; bia.Xp = 400; await _db.SaveChangesAsync();

        var dto = await _sut.GetMyLeagueAsync(ana.Id, _w1);
        Assert.Equal("Bia", dto.Leaderboard[0].Name);
        Assert.Equal(1, dto.Leaderboard[0].Rank);
        Assert.Equal("Ana", dto.Leaderboard[1].Name);
    }

    [Fact]
    public async Task Rollover_PromotesActiveTop_AndResetsBaseline()
    {
        var ana = MakeUser("Ana", 100);
        await _sut.GetMyLeagueAsync(ana.Id, _w1); // baseline 100
        ana.Xp = 400; await _db.SaveChangesAsync(); // weekly 300 > 0

        var dto = await _sut.GetMyLeagueAsync(ana.Id, W2); // vira a semana
        Assert.Equal("Prata", dto.Tier);
        Assert.Equal("promoted", dto.LastResult);
        Assert.Equal(0, dto.WeeklyXp); // baseline resetado p/ 400
    }

    [Fact]
    public async Task Rollover_InactiveStaysBronze_NoFreePromotion()
    {
        var ana = MakeUser("Ana", 100);
        await _sut.GetMyLeagueAsync(ana.Id, _w1); // baseline 100, sem ganhar XP

        var dto = await _sut.GetMyLeagueAsync(ana.Id, W2);
        Assert.Equal("Bronze", dto.Tier);
        Assert.Equal("stayed", dto.LastResult);
    }

    [Fact]
    public async Task Rollover_RelegatesBottom_WhenCohortLargeEnough()
    {
        // 12 usuários na Prata, semana A, baseline 0, weekly = i*10.
        var week = Week(_w1);
        var users = new List<User>();
        for (var i = 1; i <= 12; i++)
        {
            var u = MakeUser($"U{i:D2}", i * 10);
            users.Add(u);
            _db.UserLeague.Add(new UserLeague
            {
                UserId = u.Id, Tier = LeagueTier.Prata, WeekKey = week, BaselineXp = 0, UpdatedAt = _w1,
            });
        }
        await _db.SaveChangesAsync();

        // U01 é o pior (weekly 10) → rebaixa; U12 o melhor → promove.
        var worst = await _sut.GetMyLeagueAsync(users[0].Id, W2);
        Assert.Equal("Bronze", worst.Tier);
        Assert.Equal("relegated", worst.LastResult);

        var best = await _sut.GetMyLeagueAsync(users[11].Id, W2);
        Assert.Equal("Ouro", best.Tier);
        Assert.Equal("promoted", best.LastResult);

        var mid = await _sut.GetMyLeagueAsync(users[6].Id, W2); // U07, rank 6 de 12
        Assert.Equal("Prata", mid.Tier);
        Assert.Equal("stayed", mid.LastResult);
    }

    [Fact]
    public async Task SmallCohort_NoRelegation()
    {
        // 3 na Prata; cohort pequeno (≤ promote+relegate) → ninguém rebaixa.
        var week = Week(_w1);
        var users = new List<User>();
        for (var i = 1; i <= 3; i++)
        {
            var u = MakeUser($"S{i}", i * 10);
            users.Add(u);
            _db.UserLeague.Add(new UserLeague { UserId = u.Id, Tier = LeagueTier.Prata, WeekKey = week, BaselineXp = 0, UpdatedAt = _w1 });
        }
        await _db.SaveChangesAsync();

        var worst = await _sut.GetMyLeagueAsync(users[0].Id, W2);
        Assert.NotEqual("relegated", worst.LastResult); // promovido (top5 cobre todos com weekly>0)
        Assert.Equal("Ouro", worst.Tier);
    }

    public void Dispose() => _db.Dispose();
}
