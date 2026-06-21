using Microsoft.EntityFrameworkCore;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Persistence;
using Unravel.Infrastructure.Social;

namespace Unravel.Tests.Social;

/// <summary>
/// PR 65 — cobre a Caixinha (clã): criar (líder, 1 por user, nome curto),
/// entrar (vaga/cheia/já em uma), sair (transferência de liderança e
/// dissolução), expulsar (só líder), mural e ranking coletivo por XP.
/// EF InMemory.
/// </summary>
public class CaixinhaServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly CaixinhaService _sut;
    private readonly User _ana, _bia, _caio;

    public CaixinhaServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new CaixinhaService(_db);

        _ana  = User.Create("Ana",  Email.Create("ana@u.test"),  "h");
        _bia  = User.Create("Bia",  Email.Create("bia@u.test"),  "h");
        _caio = User.Create("Caio", Email.Create("caio@u.test"), "h");
        _ana.Xp = 100; _bia.Xp = 300; _caio.Xp = 50;
        _db.User.AddRange(_ana, _bia, _caio);
        _db.SaveChanges();
    }

    private async Task<int> CreateFor(Guid userId, string name = "Os Gatos")
    {
        var r = await _sut.CreateAsync(userId, name, "🐱");
        return r.CaixinhaId!.Value;
    }

    [Fact]
    public async Task Create_MakesLeaderAndMembership()
    {
        var r = await _sut.CreateAsync(_ana.Id, "Os Gatos", "🐱");
        Assert.Equal(CaixinhaOutcome.Ok, r.Outcome);

        var mine = await _sut.GetMineAsync(_ana.Id);
        Assert.NotNull(mine);
        Assert.Equal("Os Gatos", mine!.Name);
        Assert.Equal("Leader", mine.MyRole);
        Assert.Equal(_ana.Id, mine.LeaderId);
        Assert.Equal(1, mine.MemberCount);
    }

    [Fact]
    public async Task Create_SecondTime_Rejected()
    {
        await CreateFor(_ana.Id);
        var again = await _sut.CreateAsync(_ana.Id, "Outra", "🐈");
        Assert.Equal(CaixinhaOutcome.AlreadyInOne, again.Outcome);
    }

    [Fact]
    public async Task Create_ShortName_Rejected()
    {
        var r = await _sut.CreateAsync(_ana.Id, "x", null);
        Assert.Equal(CaixinhaOutcome.NameTooShort, r.Outcome);
    }

    [Fact]
    public async Task Join_AddsMember()
    {
        var id = await CreateFor(_ana.Id);
        var r = await _sut.JoinAsync(_bia.Id, id);
        Assert.Equal(CaixinhaOutcome.Ok, r.Outcome);

        var mine = await _sut.GetMineAsync(_bia.Id);
        Assert.Equal(2, mine!.MemberCount);
        Assert.Equal("Member", mine.MyRole);
    }

    [Fact]
    public async Task Join_WhenAlreadyInOne_Rejected()
    {
        var a = await CreateFor(_ana.Id, "Alfa");
        await CreateFor(_bia.Id, "Beta");
        var r = await _sut.JoinAsync(_bia.Id, a);
        Assert.Equal(CaixinhaOutcome.AlreadyInOne, r.Outcome);
    }

    [Fact]
    public async Task Join_NotFound()
    {
        var r = await _sut.JoinAsync(_ana.Id, 9999);
        Assert.Equal(CaixinhaOutcome.NotFound, r.Outcome);
    }

    [Fact]
    public async Task CollectivePoints_SumsMemberXp()
    {
        var id = await CreateFor(_ana.Id);   // 100
        await _sut.JoinAsync(_bia.Id, id);   // +300
        var mine = await _sut.GetMineAsync(_ana.Id);
        Assert.Equal(400, mine!.CollectivePoints);
    }

    [Fact]
    public async Task Leave_NonLeader_JustRemoves()
    {
        var id = await CreateFor(_ana.Id);
        await _sut.JoinAsync(_bia.Id, id);

        var r = await _sut.LeaveAsync(_bia.Id);
        Assert.Equal(CaixinhaOutcome.Ok, r.Outcome);
        Assert.Null(await _sut.GetMineAsync(_bia.Id));
        Assert.Equal(1, (await _sut.GetMineAsync(_ana.Id))!.MemberCount);
    }

    [Fact]
    public async Task Leave_Leader_TransfersToOldestMember()
    {
        var id = await CreateFor(_ana.Id);
        await _sut.JoinAsync(_bia.Id, id);
        await _sut.JoinAsync(_caio.Id, id);

        var r = await _sut.LeaveAsync(_ana.Id);
        Assert.Equal(CaixinhaOutcome.Ok, r.Outcome);

        // Bia entrou antes de Caio → vira líder.
        var biaView = await _sut.GetMineAsync(_bia.Id);
        Assert.Equal("Leader", biaView!.MyRole);
        Assert.Equal(_bia.Id, biaView.LeaderId);
    }

    [Fact]
    public async Task Leave_LastMemberLeader_Disbands()
    {
        var id = await CreateFor(_ana.Id);
        var r = await _sut.LeaveAsync(_ana.Id);
        Assert.Equal(CaixinhaOutcome.Disbanded, r.Outcome);
        Assert.False(await _db.Caixinha.AnyAsync(c => c.Id == id));
    }

    [Fact]
    public async Task Kick_ByLeader_Removes()
    {
        var id = await CreateFor(_ana.Id);
        await _sut.JoinAsync(_bia.Id, id);

        var r = await _sut.KickAsync(_ana.Id, _bia.Id);
        Assert.Equal(CaixinhaOutcome.Ok, r.Outcome);
        Assert.Null(await _sut.GetMineAsync(_bia.Id));
    }

    [Fact]
    public async Task Kick_ByNonLeader_Forbidden()
    {
        var id = await CreateFor(_ana.Id);
        await _sut.JoinAsync(_bia.Id, id);
        await _sut.JoinAsync(_caio.Id, id);

        var r = await _sut.KickAsync(_bia.Id, _caio.Id); // Bia não é líder
        Assert.Equal(CaixinhaOutcome.NotLeader, r.Outcome);
    }

    [Fact]
    public async Task PostMessage_AppearsInMural()
    {
        await CreateFor(_ana.Id);
        var r = await _sut.PostMessageAsync(_ana.Id, "Bora estudar hoje! 🐾");
        Assert.Equal(CaixinhaOutcome.Ok, r.Outcome);

        var mine = await _sut.GetMineAsync(_ana.Id);
        var msg = Assert.Single(mine!.Mural);
        Assert.Equal("Bora estudar hoje! 🐾", msg.Text);
        Assert.Equal("Ana", msg.AuthorName);
    }

    [Fact]
    public async Task PostMessage_NotInAny_Rejected()
    {
        var r = await _sut.PostMessageAsync(_ana.Id, "oi");
        Assert.Equal(CaixinhaOutcome.NotInAny, r.Outcome);
    }

    [Fact]
    public async Task Leaderboard_OrdersByCollectivePoints()
    {
        var a = await CreateFor(_ana.Id, "Fracos");   // Ana 100
        var b = await CreateFor(_bia.Id, "Fortes");   // Bia 300
        await _sut.JoinAsync(_caio.Id, a);            // Fracos += 50 = 150

        var lb = await _sut.LeaderboardAsync(10);
        Assert.Equal("Fortes", lb[0].Name);  // 300
        Assert.Equal(1, lb[0].Rank);
        Assert.Equal("Fracos", lb[1].Name);  // 150
        Assert.Equal(2, lb[1].Rank);
    }

    public void Dispose() => _db.Dispose();
}
