using Microsoft.EntityFrameworkCore;
using Unravel.Application.Achievements.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Achievements;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Achievements;

/// <summary>Títulos desbloqueáveis (Ideia 5): catálogo auto-seed, concessão por
/// critério, ativação só do que se possui, ranking global. EF InMemory.</summary>
public class TitleServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly TitleService _sut;
    private readonly DateTime _now = new(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);

    public TitleServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new TitleService(_db);
    }

    private User AddUser(string name, int streak = 0, int xp = 0)
    {
        var u = User.Create(name, Email.Create($"{name}{Guid.NewGuid():N}@u.test"), "h");
        u.StreakDays = streak; u.Xp = xp;
        _db.User.Add(u);
        _db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task List_seeds_catalog()
    {
        var u = AddUser("Ana");
        var list = await _sut.ListAsync(u.Id);
        Assert.NotEmpty(list);
        Assert.All(list, t => Assert.False(t.Owned));
    }

    [Fact]
    public async Task Evaluate_grants_by_streak_and_is_idempotent()
    {
        var u = AddUser("Ana", streak: 30);
        var granted = await _sut.EvaluateAsync(u.Id, _now);
        // streak-7 e streak-30 (não streak-100)
        Assert.Contains("Gato Persistente", granted);
        Assert.Contains("Maine Coon do Hábito", granted);
        Assert.DoesNotContain("Lendário das 100 Noites", granted);

        var again = await _sut.EvaluateAsync(u.Id, _now);
        Assert.Empty(again); // idempotente
    }

    [Fact]
    public async Task Evaluate_grants_by_xp()
    {
        var u = AddUser("Bia", xp: 1500);
        var granted = await _sut.EvaluateAsync(u.Id, _now);
        Assert.Contains("CSSiamês Profissional", granted);   // xp>=1000
        Assert.DoesNotContain("Mestre dos Bits", granted);   // xp<5000
    }

    [Fact]
    public async Task Activate_requires_ownership_then_sets_active_title()
    {
        var u = AddUser("Ana", streak: 10);
        await _sut.EvaluateAsync(u.Id, _now); // ganha streak-7
        var titles = await _sut.ListAsync(u.Id);
        var owned = titles.First(t => t.Owned);
        var notOwned = titles.First(t => !t.Owned);

        Assert.Equal(ActivateTitleOutcome.NotOwned, await _sut.ActivateAsync(u.Id, notOwned.Id));
        Assert.Equal(ActivateTitleOutcome.Ok, await _sut.ActivateAsync(u.Id, owned.Id));

        var user = await _db.User.AsNoTracking().FirstAsync(x => x.Id == u.Id);
        Assert.Equal(owned.Text, user.ActiveTitle);

        // id 0 limpa
        Assert.Equal(ActivateTitleOutcome.Ok, await _sut.ActivateAsync(u.Id, 0));
        Assert.Null((await _db.User.AsNoTracking().FirstAsync(x => x.Id == u.Id)).ActiveTitle);
    }

    [Fact]
    public async Task GlobalRanking_orders_by_xp()
    {
        var a = AddUser("Ana", xp: 100);
        var b = AddUser("Bia", xp: 900);
        var ranking = await _sut.GlobalRankingAsync(10);
        Assert.Equal(b.Id, ranking[0].UserId);
        Assert.Equal(1, ranking[0].Rank);
        Assert.Equal(a.Id, ranking[1].UserId);
    }

    public void Dispose() => _db.Dispose();
}
