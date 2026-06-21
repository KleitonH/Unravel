using Microsoft.EntityFrameworkCore;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Notifications;
using Unravel.Infrastructure.Persistence;
using Unravel.Infrastructure.Social;

namespace Unravel.Tests.Social;

/// <summary>
/// PR 64 — cobre o grafo de amizades: enviar (dedup nas duas direções,
/// self), aceitar/recusar (autorização), listar amigos (placar por XP),
/// pedidos pendentes, busca (relação anotada) e remover. EF InMemory.
/// </summary>
public class FriendshipServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly FriendshipService _sut;
    private readonly User _ana;
    private readonly User _bia;
    private readonly User _caio;

    public FriendshipServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new FriendshipService(_db, new NotificationService(_db));

        _ana  = User.Create("Ana Silva",   Email.Create("ana@unravel.test"),  "hash");
        _bia  = User.Create("Bia Souza",   Email.Create("bia@unravel.test"),  "hash");
        _caio = User.Create("Caio Santos", Email.Create("caio@unravel.test"), "hash");
        _ana.Xp = 100; _bia.Xp = 300; _caio.Xp = 200;
        _db.User.AddRange(_ana, _bia, _caio);
        _db.SaveChanges();
    }

    [Fact]
    public async Task Send_CreatesPending()
    {
        var r = await _sut.SendRequestAsync(_ana.Id, _bia.Id);

        Assert.Equal(FriendActionOutcome.Ok, r.Outcome);
        var f = await _db.Friendship.SingleAsync();
        Assert.Equal(FriendshipStatus.Pending, f.Status);
        Assert.Equal(_ana.Id, f.RequesterId);
        Assert.Equal(_bia.Id, f.AddresseeId);
    }

    [Fact]
    public async Task Send_ToSelf_Rejected()
    {
        var r = await _sut.SendRequestAsync(_ana.Id, _ana.Id);
        Assert.Equal(FriendActionOutcome.CannotSelf, r.Outcome);
        Assert.Equal(0, await _db.Friendship.CountAsync());
    }

    [Fact]
    public async Task Send_DuplicateEitherDirection_RejectedAsPending()
    {
        await _sut.SendRequestAsync(_ana.Id, _bia.Id);

        var same    = await _sut.SendRequestAsync(_ana.Id, _bia.Id);
        var reverse = await _sut.SendRequestAsync(_bia.Id, _ana.Id);

        Assert.Equal(FriendActionOutcome.AlreadyPending, same.Outcome);
        Assert.Equal(FriendActionOutcome.AlreadyPending, reverse.Outcome);
        Assert.Equal(1, await _db.Friendship.CountAsync());
    }

    [Fact]
    public async Task Accept_MakesThemFriends_BothDirections()
    {
        var send = await _sut.SendRequestAsync(_ana.Id, _bia.Id);

        var resp = await _sut.RespondAsync(_bia.Id, send.FriendshipId!.Value, accept: true);
        Assert.Equal(FriendActionOutcome.Ok, resp.Outcome);

        var anaFriends = await _sut.GetFriendsAsync(_ana.Id);
        var biaFriends = await _sut.GetFriendsAsync(_bia.Id);
        Assert.Equal(_bia.Id, Assert.Single(anaFriends).UserId);
        Assert.Equal(_ana.Id, Assert.Single(biaFriends).UserId);
    }

    [Fact]
    public async Task SendAfterFriends_ReturnsAlreadyFriends()
    {
        var send = await _sut.SendRequestAsync(_ana.Id, _bia.Id);
        await _sut.RespondAsync(_bia.Id, send.FriendshipId!.Value, accept: true);

        var again = await _sut.SendRequestAsync(_ana.Id, _bia.Id);
        Assert.Equal(FriendActionOutcome.AlreadyFriends, again.Outcome);
    }

    [Fact]
    public async Task Decline_RemovesAndAllowsResend()
    {
        var send = await _sut.SendRequestAsync(_ana.Id, _bia.Id);

        var resp = await _sut.RespondAsync(_bia.Id, send.FriendshipId!.Value, accept: false);
        Assert.Equal(FriendActionOutcome.Ok, resp.Outcome);
        Assert.Equal(0, await _db.Friendship.CountAsync());

        var resend = await _sut.SendRequestAsync(_ana.Id, _bia.Id);
        Assert.Equal(FriendActionOutcome.Ok, resend.Outcome);
    }

    [Fact]
    public async Task Respond_ByNonAddressee_NotAuthorized()
    {
        var send = await _sut.SendRequestAsync(_ana.Id, _bia.Id);

        // Quem enviou (Ana) não pode aceitar o próprio pedido.
        var resp = await _sut.RespondAsync(_ana.Id, send.FriendshipId!.Value, accept: true);
        Assert.Equal(FriendActionOutcome.NotAuthorized, resp.Outcome);
    }

    [Fact]
    public async Task GetRequests_SplitsIncomingOutgoing()
    {
        await _sut.SendRequestAsync(_ana.Id, _bia.Id);  // ana → bia
        await _sut.SendRequestAsync(_caio.Id, _ana.Id); // caio → ana

        var reqs = await _sut.GetRequestsAsync(_ana.Id);
        Assert.Equal(_caio.Id, Assert.Single(reqs.Incoming).UserId);
        Assert.Equal(_bia.Id,  Assert.Single(reqs.Outgoing).UserId);
    }

    [Fact]
    public async Task GetFriends_OrderedByXpDesc()
    {
        // Ana vira amiga de Bia (xp 300) e Caio (xp 200).
        var s1 = await _sut.SendRequestAsync(_ana.Id, _bia.Id);
        await _sut.RespondAsync(_bia.Id, s1.FriendshipId!.Value, accept: true);
        var s2 = await _sut.SendRequestAsync(_ana.Id, _caio.Id);
        await _sut.RespondAsync(_caio.Id, s2.FriendshipId!.Value, accept: true);

        var friends = await _sut.GetFriendsAsync(_ana.Id);
        Assert.Collection(friends,
            f => Assert.Equal(_bia.Id, f.UserId),
            f => Assert.Equal(_caio.Id, f.UserId));
    }

    [Fact]
    public async Task Search_MatchesName_ExcludesSelf_AnnotatesRelation()
    {
        await _sut.SendRequestAsync(_ana.Id, _bia.Id); // pending_out vs Bia

        var results = await _sut.SearchAsync(_ana.Id, "a", take: 20); // "a" curto demais
        Assert.Empty(results);

        var found = await _sut.SearchAsync(_ana.Id, "Sou", take: 20); // casa "Bia Souza"
        var bia = Assert.Single(found, u => u.UserId == _bia.Id);
        Assert.Equal("pending_out", bia.RelationStatus);
        Assert.DoesNotContain(found, u => u.UserId == _ana.Id);
    }

    [Fact]
    public async Task Remove_DeletesFriendshipEitherDirection()
    {
        var send = await _sut.SendRequestAsync(_ana.Id, _bia.Id);
        await _sut.RespondAsync(_bia.Id, send.FriendshipId!.Value, accept: true);

        var r = await _sut.RemoveAsync(_bia.Id, _ana.Id); // remove pela outra ponta
        Assert.Equal(FriendActionOutcome.Ok, r.Outcome);
        Assert.Equal(0, await _db.Friendship.CountAsync());
    }

    public void Dispose() => _db.Dispose();
}
