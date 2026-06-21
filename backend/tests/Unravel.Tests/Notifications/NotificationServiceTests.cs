using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Notifications;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Notifications;

/// <summary>PR 69 — central de notificações: criar, listar (desc), contador de
/// não-lidas, marcar lida e marcar todas. EF InMemory.</summary>
public class NotificationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly NotificationService _sut;
    private readonly Guid _u = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new NotificationService(_db);
    }

    [Fact]
    public async Task Create_And_List_And_UnreadCount()
    {
        await _sut.CreateAsync(_u, NotificationType.FriendRequest, "T1", "B1", "/amigos");
        await _sut.CreateAsync(_u, NotificationType.CaixinhaGoal, "T2", "B2", "/caixinha");
        await _sut.CreateAsync(_other, NotificationType.System, "X", "Y");

        var list = await _sut.ListAsync(_u, 30);
        Assert.Equal(2, list.Count);                 // só do usuário
        Assert.Equal("T2", list[0].Title);           // mais recente primeiro
        Assert.Equal(2, await _sut.UnreadCountAsync(_u));
    }

    [Fact]
    public async Task CreateMany_FanOut()
    {
        await _sut.CreateManyAsync([_u, _other, _u], NotificationType.CaixinhaGoal, "Meta", "bateu");
        Assert.Equal(1, await _sut.UnreadCountAsync(_u));     // dedup do _u
        Assert.Equal(1, await _sut.UnreadCountAsync(_other));
    }

    [Fact]
    public async Task MarkRead_DecrementsUnread()
    {
        await _sut.CreateAsync(_u, NotificationType.System, "T", "B");
        var id = (await _sut.ListAsync(_u, 30))[0].Id;

        await _sut.MarkReadAsync(_u, id);
        Assert.Equal(0, await _sut.UnreadCountAsync(_u));
    }

    [Fact]
    public async Task MarkRead_OtherUser_NoEffect()
    {
        await _sut.CreateAsync(_u, NotificationType.System, "T", "B");
        var id = (await _sut.ListAsync(_u, 30))[0].Id;

        await _sut.MarkReadAsync(_other, id); // não é dono
        Assert.Equal(1, await _sut.UnreadCountAsync(_u));
    }

    [Fact]
    public async Task MarkAllRead_ZeroesUnread()
    {
        await _sut.CreateAsync(_u, NotificationType.System, "A", "a");
        await _sut.CreateAsync(_u, NotificationType.System, "B", "b");
        await _sut.MarkAllReadAsync(_u);
        Assert.Equal(0, await _sut.UnreadCountAsync(_u));
    }

    public void Dispose() => _db.Dispose();
}
