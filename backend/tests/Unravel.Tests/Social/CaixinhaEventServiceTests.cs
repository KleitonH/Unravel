using Microsoft.EntityFrameworkCore;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Persistence;
using Unravel.Infrastructure.Social;

namespace Unravel.Tests.Social;

/// <summary>
/// PR 65c — eventos entre caixinhas: criar (datas/nome), status derivado de
/// `now`, participar (líder/snapshot baseline), ranking ao vivo (atual−baseline)
/// e congelamento ao encerrar. EF InMemory.
/// </summary>
public class CaixinhaEventServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly CaixinhaEventService _sut;
    private readonly User _ana, _bia;
    private readonly DateTime _t0 = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public CaixinhaEventServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new CaixinhaEventService(_db);

        _ana = User.Create("Ana", Email.Create("ana@u.test"), "h");
        _bia = User.Create("Bia", Email.Create("bia@u.test"), "h");
        _ana.Xp = 100; _bia.Xp = 100;
        _db.User.AddRange(_ana, _bia);
        _db.SaveChanges();
    }

    private int MakeCaixinha(string name, User leader, params User[] members)
    {
        var c = new Caixinha { Name = name, Emblem = "🐱", LeaderId = leader.Id };
        _db.Caixinha.Add(c);
        _db.SaveChanges();
        _db.CaixinhaMember.Add(new CaixinhaMember { CaixinhaId = c.Id, UserId = leader.Id, Role = CaixinhaRole.Leader });
        foreach (var m in members)
            _db.CaixinhaMember.Add(new CaixinhaMember { CaixinhaId = c.Id, UserId = m.Id, Role = CaixinhaRole.Member });
        _db.SaveChanges();
        return c.Id;
    }

    [Fact]
    public async Task Create_Ok()
    {
        var r = await _sut.CreateAsync(_ana.Id, "Semana de Backend", "Backend", _t0, _t0.AddDays(5));
        Assert.Equal(CaixinhaEventOutcome.Ok, r.Outcome);
        Assert.NotNull(r.EventId);
    }

    [Fact]
    public async Task Create_InvalidDates_Rejected()
    {
        var r = await _sut.CreateAsync(_ana.Id, "X Event", null, _t0.AddDays(5), _t0);
        Assert.Equal(CaixinhaEventOutcome.InvalidDates, r.Outcome);
    }

    [Fact]
    public async Task List_DerivesStatusFromNow()
    {
        await _sut.CreateAsync(_ana.Id, "Passado", null, _t0.AddDays(-10), _t0.AddDays(-5));
        await _sut.CreateAsync(_ana.Id, "Agora",   null, _t0.AddDays(-1), _t0.AddDays(2));
        await _sut.CreateAsync(_ana.Id, "Futuro",  null, _t0.AddDays(3),  _t0.AddDays(7));

        var list = await _sut.ListAsync(_ana.Id, _t0);
        // ativo primeiro
        Assert.Equal("active", list[0].Status);
        Assert.Equal("Agora", list[0].Name);
        Assert.Contains(list, e => e.Name == "Futuro"  && e.Status == "upcoming");
        Assert.Contains(list, e => e.Name == "Passado" && e.Status == "finished");
    }

    [Fact]
    public async Task Join_Leader_SnapshotsBaseline()
    {
        var id = MakeCaixinha("Gatos", _ana, _bia); // 200 pts
        var ev = await _sut.CreateAsync(_ana.Id, "Evento", null, _t0, _t0.AddDays(5));

        var r = await _sut.JoinAsync(_ana.Id, ev.EventId!.Value, _t0.AddDays(1));
        Assert.Equal(CaixinhaEventOutcome.Ok, r.Outcome);

        var score = await _db.CaixinhaEventScore.SingleAsync();
        Assert.Equal(200, score.BaselinePoints);
        Assert.Equal(id, score.CaixinhaId);
    }

    [Fact]
    public async Task Join_NonLeader_Rejected()
    {
        MakeCaixinha("Gatos", _ana, _bia);
        var ev = await _sut.CreateAsync(_ana.Id, "Evento", null, _t0, _t0.AddDays(5));

        var r = await _sut.JoinAsync(_bia.Id, ev.EventId!.Value, _t0.AddDays(1));
        Assert.Equal(CaixinhaEventOutcome.NotLeader, r.Outcome);
    }

    [Fact]
    public async Task Join_NotInAny_Rejected()
    {
        var ev = await _sut.CreateAsync(_ana.Id, "Evento", null, _t0, _t0.AddDays(5));
        var r = await _sut.JoinAsync(_ana.Id, ev.EventId!.Value, _t0.AddDays(1));
        Assert.Equal(CaixinhaEventOutcome.NotInAny, r.Outcome);
    }

    [Fact]
    public async Task Join_NotActive_Rejected()
    {
        MakeCaixinha("Gatos", _ana);
        var ev = await _sut.CreateAsync(_ana.Id, "Futuro", null, _t0.AddDays(3), _t0.AddDays(7));
        var r = await _sut.JoinAsync(_ana.Id, ev.EventId!.Value, _t0); // antes de começar
        Assert.Equal(CaixinhaEventOutcome.NotActive, r.Outcome);
    }

    [Fact]
    public async Task Join_Twice_Rejected()
    {
        MakeCaixinha("Gatos", _ana);
        var ev = await _sut.CreateAsync(_ana.Id, "Evento", null, _t0, _t0.AddDays(5));
        await _sut.JoinAsync(_ana.Id, ev.EventId!.Value, _t0.AddDays(1));
        var again = await _sut.JoinAsync(_ana.Id, ev.EventId!.Value, _t0.AddDays(1));
        Assert.Equal(CaixinhaEventOutcome.AlreadyJoined, again.Outcome);
    }

    [Fact]
    public async Task Ranking_LivePoints_AreCurrentMinusBaseline()
    {
        var id = MakeCaixinha("Gatos", _ana, _bia); // baseline 200
        var ev = await _sut.CreateAsync(_ana.Id, "Evento", null, _t0, _t0.AddDays(5));
        await _sut.JoinAsync(_ana.Id, ev.EventId!.Value, _t0.AddDays(1));

        // membros ganham XP durante o evento: 200 → 350
        _ana.Xp = 250; _bia.Xp = 100;
        await _db.SaveChangesAsync();

        var detail = await _sut.GetDetailAsync(_ana.Id, ev.EventId.Value, _t0.AddDays(2));
        var entry = Assert.Single(detail!.Ranking);
        Assert.Equal(id, entry.CaixinhaId);
        Assert.Equal(150, entry.Points); // 350 − 200
        Assert.True(entry.IsMine);
    }

    [Fact]
    public async Task Ranking_FreezesAfterEnd()
    {
        MakeCaixinha("Gatos", _ana, _bia); // baseline 200
        var ev = await _sut.CreateAsync(_ana.Id, "Evento", null, _t0, _t0.AddDays(5));
        await _sut.JoinAsync(_ana.Id, ev.EventId!.Value, _t0.AddDays(1));

        _ana.Xp = 400; // 200 → 500 durante o evento
        await _db.SaveChangesAsync();

        // primeira leitura PÓS-fim congela em 300
        var afterEnd = await _sut.GetDetailAsync(_ana.Id, ev.EventId.Value, _t0.AddDays(6));
        Assert.Equal(300, afterEnd!.Ranking[0].Points);

        // ganha mais XP depois do fim — não deve contar
        _ana.Xp = 900;
        await _db.SaveChangesAsync();
        var later = await _sut.GetDetailAsync(_ana.Id, ev.EventId.Value, _t0.AddDays(7));
        Assert.Equal(300, later!.Ranking[0].Points); // congelado
    }

    [Fact]
    public async Task Ranking_OrdersByPointsDesc()
    {
        var g1 = MakeCaixinha("Time A", _ana); // baseline 100
        var g2 = MakeCaixinha("Time B", _bia); // baseline 100
        var ev = await _sut.CreateAsync(_ana.Id, "Evento", null, _t0, _t0.AddDays(5));
        await _sut.JoinAsync(_ana.Id, ev.EventId!.Value, _t0.AddDays(1));
        await _sut.JoinAsync(_bia.Id, ev.EventId.Value, _t0.AddDays(1));

        _ana.Xp = 150;  // Time A: +50
        _bia.Xp = 400;  // Time B: +300
        await _db.SaveChangesAsync();

        var detail = await _sut.GetDetailAsync(_ana.Id, ev.EventId.Value, _t0.AddDays(2));
        Assert.Equal("Time B", detail!.Ranking[0].Name);
        Assert.Equal(1, detail.Ranking[0].Rank);
        Assert.Equal("Time A", detail.Ranking[1].Name);
    }

    public void Dispose() => _db.Dispose();
}
