using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Domain.Gamification;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Gamification;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Gamification;

/// <summary>UC34 — serviço de recarga (lazy por usuário + lote do cron). EF InMemory.</summary>
public class LifeRefillServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly LifeRefillService _sut;
    private readonly DateTime _now = new(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);

    public LifeRefillServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new LifeRefillService(_db);
    }

    private User AddUser(int lives, DateTime? anchor)
    {
        var u = User.Create("U", Email.Create($"{Guid.NewGuid():N}@u.test"), "h"); // IsActive=true por padrão
        u.Lives = lives; u.LastLifeRefillAt = anchor;
        _db.User.Add(u);
        _db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task RefillUser_grants_and_persists_anchor()
    {
        var u = AddUser(5, _now.AddHours(-2));
        var granted = await _sut.RefillUserAsync(u.Id, _now);

        Assert.Equal(2, granted);
        var fresh = await _db.User.AsNoTracking().FirstAsync(x => x.Id == u.Id);
        Assert.Equal(7, fresh.Lives);
        Assert.Equal(_now, fresh.LastLifeRefillAt); // chegou ao teto → relógio zerado
    }

    [Fact]
    public async Task RefillUser_at_cap_returns_zero()
    {
        var u = AddUser(LifeRefill.MaxLives, _now.AddHours(-3));
        Assert.Equal(0, await _sut.RefillUserAsync(u.Id, _now));
    }

    [Fact]
    public async Task RefillAll_affects_only_users_below_cap()
    {
        var a = AddUser(3, _now.AddHours(-1));            // ganha 1
        var b = AddUser(LifeRefill.MaxLives, _now.AddHours(-5)); // no teto, ignorado
        var c = AddUser(6, _now.AddMinutes(-30));         // <1h, sem ganho

        var affected = await _sut.RefillAllAsync(_now);

        Assert.Equal(1, affected);
        Assert.Equal(4, (await _db.User.AsNoTracking().FirstAsync(x => x.Id == a.Id)).Lives);
        Assert.Equal(LifeRefill.MaxLives, (await _db.User.AsNoTracking().FirstAsync(x => x.Id == b.Id)).Lives);
        Assert.Equal(6, (await _db.User.AsNoTracking().FirstAsync(x => x.Id == c.Id)).Lives);
    }

    public void Dispose() => _db.Dispose();
}
