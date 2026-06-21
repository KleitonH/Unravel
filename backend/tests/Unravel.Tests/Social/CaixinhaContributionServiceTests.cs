using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Persistence;
using Unravel.Infrastructure.Social;

namespace Unravel.Tests.Social;

/// <summary>
/// PR 67 — meta coletiva diária: acumula XP do dia, reseta na virada e dá
/// bônus de moedas a todos os membros uma única vez quando bate a meta.
/// EF InMemory.
/// </summary>
public class CaixinhaContributionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly CaixinhaContributionService _sut;
    private readonly User _ana, _bia;
    private readonly int _caixinhaId;
    private readonly DateTime _now = new(2026, 6, 10, 14, 0, 0, DateTimeKind.Utc);

    public CaixinhaContributionServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new CaixinhaContributionService(_db);

        _ana = User.Create("Ana", Email.Create("ana@u.test"), "h");
        _bia = User.Create("Bia", Email.Create("bia@u.test"), "h");
        _db.User.AddRange(_ana, _bia);

        var c = new Caixinha { Name = "Gatos", Emblem = "🐱", LeaderId = _ana.Id, DailyGoal = 100 };
        _db.Caixinha.Add(c);
        _db.SaveChanges();
        _caixinhaId = c.Id;
        _db.CaixinhaMember.AddRange(
            new CaixinhaMember { CaixinhaId = c.Id, UserId = _ana.Id, Role = CaixinhaRole.Leader },
            new CaixinhaMember { CaixinhaId = c.Id, UserId = _bia.Id, Role = CaixinhaRole.Member });
        _db.SaveChanges();
    }

    private async Task<Caixinha> Reload() =>
        await _db.Caixinha.AsNoTracking().FirstAsync(c => c.Id == _caixinhaId);

    [Fact]
    public async Task Contribute_AccumulatesDailyPoints()
    {
        await _sut.ContributeAsync(_ana.Id, 30, _now);
        var c = await Reload();
        Assert.Equal(30, c.DailyPoints);
        Assert.Equal(_now.Date, c.DailyPointsDate?.Date);
    }

    [Fact]
    public async Task Contribute_NewDay_Resets()
    {
        await _sut.ContributeAsync(_ana.Id, 50, _now);
        await _sut.ContributeAsync(_ana.Id, 20, _now.AddDays(1));
        var c = await Reload();
        Assert.Equal(20, c.DailyPoints); // só o dia novo
    }

    [Fact]
    public async Task Contribute_ReachesGoal_AwardsAllMembersOnce()
    {
        await _sut.ContributeAsync(_ana.Id, 60, _now); // 60 < 100
        Assert.Null((await Reload()).DailyGoalAwardedAt);

        await _sut.ContributeAsync(_bia.Id, 60, _now); // 120 ≥ 100 → bônus
        var c = await Reload();
        Assert.NotNull(c.DailyGoalAwardedAt);
        Assert.Equal(CaixinhaContributionService.DailyBonusCoins, (await _db.User.AsNoTracking().FirstAsync(u => u.Id == _ana.Id)).Coins);
        Assert.Equal(CaixinhaContributionService.DailyBonusCoins, (await _db.User.AsNoTracking().FirstAsync(u => u.Id == _bia.Id)).Coins);

        // não premia de novo no mesmo dia
        await _sut.ContributeAsync(_ana.Id, 50, _now);
        Assert.Equal(CaixinhaContributionService.DailyBonusCoins, (await _db.User.AsNoTracking().FirstAsync(u => u.Id == _ana.Id)).Coins);
    }

    [Fact]
    public async Task Contribute_UserWithoutCaixinha_NoOp()
    {
        var solo = User.Create("Solo", Email.Create("solo@u.test"), "h");
        _db.User.Add(solo); await _db.SaveChangesAsync();

        await _sut.ContributeAsync(solo.Id, 100, _now); // não lança
        Assert.Equal(0, solo.Coins);
    }

    [Fact]
    public async Task Contribute_NonPositiveXp_NoOp()
    {
        await _sut.ContributeAsync(_ana.Id, 0, _now);
        Assert.Equal(0, (await Reload()).DailyPoints);
    }

    public void Dispose() => _db.Dispose();
}
