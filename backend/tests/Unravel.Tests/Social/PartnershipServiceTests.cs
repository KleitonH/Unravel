using Microsoft.EntityFrameworkCore;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Notifications;
using Unravel.Infrastructure.Persistence;
using Unravel.Infrastructure.Social;

namespace Unravel.Tests.Social;

/// <summary>
/// Ideia 1 / UC17 — Parceria ("Novelo de Lã / Parceiro de Trilha"). Cobre:
/// pré-condição de amizade, aceite que cria o primeiro novelo, passagem do
/// novelo ao cumprir a meta, conclusão com recompensa, teto de 3 parcerias e
/// enrola/derruba por inatividade. EF InMemory.
/// </summary>
public class PartnershipServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly PartnershipService _sut;
    private readonly User _ana;
    private readonly User _bia;
    private readonly DateTime _now = new(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);

    public PartnershipServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new PartnershipService(_db, new NotificationService(_db));

        _ana = AddUser("Ana");
        _bia = AddUser("Bia");
    }

    private User AddUser(string name)
    {
        var u = User.Create(name, Email.Create($"{name}{Guid.NewGuid():N}@u.test"), "h");
        _db.User.Add(u);
        _db.SaveChanges();
        return u;
    }

    private void MakeFriends(Guid a, Guid b)
    {
        _db.Friendship.Add(new Friendship { RequesterId = a, AddresseeId = b, Status = FriendshipStatus.Accepted });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Request_requires_friendship()
    {
        var r = await _sut.RequestAsync(_ana.Id, _bia.Id);
        Assert.Equal(PartnershipOutcome.NotFriends, r.Outcome);
        Assert.Equal(0, await _db.Partnership.CountAsync());
    }

    [Fact]
    public async Task Accept_creates_active_partnership_with_first_yarn_owned_by_requester()
    {
        MakeFriends(_ana.Id, _bia.Id);
        var req = await _sut.RequestAsync(_ana.Id, _bia.Id);
        Assert.Equal(PartnershipOutcome.Ok, req.Outcome);

        var resp = await _sut.RespondAsync(_bia.Id, req.PartnershipId, accept: true);
        Assert.Equal(PartnershipOutcome.Ok, resp.Outcome);

        var p = await _db.Partnership.SingleAsync();
        Assert.Equal(PartnershipStatus.Active, p.Status);
        var yarn = await _db.YarnBall.SingleAsync(y => !y.IsCompleted);
        Assert.Equal(_ana.Id, yarn.CurrentOwnerId); // começa com quem convidou
    }

    [Fact]
    public async Task AddProgress_passes_yarn_to_partner_when_goal_met()
    {
        MakeFriends(_ana.Id, _bia.Id);
        var req = await _sut.RequestAsync(_ana.Id, _bia.Id);
        await _sut.RespondAsync(_bia.Id, req.PartnershipId, accept: true);

        var yarn = await _db.YarnBall.FirstAsync(y => !y.IsCompleted); // DailyGoal=3
        var r = await _sut.AddProgressAsync(_ana.Id, yarn.DailyGoal);

        Assert.True(r.Passed);
        Assert.False(r.NoveloCompleted);
        var after = await _db.YarnBall.AsNoTracking().FirstAsync(y => y.Id == yarn.Id);
        Assert.Equal(_bia.Id, after.CurrentOwnerId); // passou pro parceiro
        Assert.Equal(1, after.CyclesCompleted);
        Assert.Equal(0, after.Progress);
    }

    [Fact]
    public async Task AddProgress_completes_novelo_and_rewards_both()
    {
        MakeFriends(_ana.Id, _bia.Id);
        var req = await _sut.RequestAsync(_ana.Id, _bia.Id);
        await _sut.RespondAsync(_bia.Id, req.PartnershipId, accept: true);

        // Encurta o novelo pra fechar em 2 ciclos de 1.
        var yarn = await _db.YarnBall.FirstAsync(y => !y.IsCompleted);
        yarn.DailyGoal = 1; yarn.TotalCycles = 2;
        await _db.SaveChangesAsync();

        var r = await _sut.AddProgressAsync(_ana.Id, 2);
        Assert.True(r.NoveloCompleted);

        var p = await _db.Partnership.AsNoTracking().SingleAsync();
        Assert.Equal(1, p.NovelosCompleted);

        // Ambos recompensados (+100 moedas).
        var ana = await _db.User.AsNoTracking().FirstAsync(u => u.Id == _ana.Id);
        var bia = await _db.User.AsNoTracking().FirstAsync(u => u.Id == _bia.Id);
        Assert.Equal(100, ana.Coins);
        Assert.Equal(100, bia.Coins);

        // Novo novelo recomeçou (1 concluído + 1 ativo).
        Assert.Equal(1, await _db.YarnBall.CountAsync(y => !y.IsCompleted));
        Assert.Equal(1, await _db.YarnBall.CountAsync(y => y.IsCompleted));
    }

    [Fact]
    public async Task Request_rejected_when_limit_reached()
    {
        // Ana já tem 3 parcerias ativas (seed direto).
        for (var i = 0; i < 3; i++)
        {
            var other = AddUser($"P{i}");
            _db.Partnership.Add(new Partnership
            {
                RequesterId = _ana.Id, AddresseeId = other.Id, Status = PartnershipStatus.Active,
            });
        }
        await _db.SaveChangesAsync();

        MakeFriends(_ana.Id, _bia.Id);
        var r = await _sut.RequestAsync(_ana.Id, _bia.Id);
        Assert.Equal(PartnershipOutcome.LimitReached, r.Outcome);
    }

    [Fact]
    public async Task EvaluateInactivity_tangles_then_drops()
    {
        MakeFriends(_ana.Id, _bia.Id);
        var req = await _sut.RequestAsync(_ana.Id, _bia.Id);
        await _sut.RespondAsync(_bia.Id, req.PartnershipId, accept: true);

        var yarn = await _db.YarnBall.FirstAsync(y => !y.IsCompleted);
        yarn.LastProgressAt = _now.AddDays(-1.2); // 1+ dia parado
        await _db.SaveChangesAsync();

        var tangled = await _sut.EvaluateInactivityAsync(_now);
        Assert.Equal(1, tangled);
        Assert.Equal(YarnState.Tangled, (await _db.YarnBall.AsNoTracking().FirstAsync(y => y.Id == yarn.Id)).State);

        // Agora 2+ dias parado → cai e gera novelo novo.
        var y2 = await _db.YarnBall.FirstAsync(y => y.Id == yarn.Id);
        y2.LastProgressAt = _now.AddDays(-3);
        await _db.SaveChangesAsync();

        var dropped = await _sut.EvaluateInactivityAsync(_now);
        Assert.Equal(1, dropped);
        Assert.Equal(YarnState.Dropped, (await _db.YarnBall.AsNoTracking().FirstAsync(y => y.Id == yarn.Id)).State);
        Assert.Equal(1, await _db.YarnBall.CountAsync(y => !y.IsCompleted && y.PartnershipId == req.PartnershipId));
    }

    public void Dispose() => _db.Dispose();
}
