using Microsoft.EntityFrameworkCore;
using Unravel.Application.Notifications.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Notifications;

/// <summary>PR 69 — notificações in-app (EF). Producers chamam Create*; o sino
/// consome List/UnreadCount/MarkRead.</summary>
public class NotificationService(ApplicationDbContext db) : INotificationService
{
    public Task CreateAsync(Guid userId, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default)
        => CreateManyAsync([userId], type, title, body, link, ct);

    public async Task CreateManyAsync(IEnumerable<Guid> userIds, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var uid in userIds.Distinct())
        {
            db.Notification.Add(new Notification
            {
                UserId = uid, Type = type, Title = title, Body = body, Link = link, CreatedAt = now,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, int take, CancellationToken ct = default)
    {
        var rows = await db.Notification.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take <= 0 ? 30 : Math.Min(take, 100))
            .Select(n => new { n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt })
            .ToListAsync(ct);

        return rows
            .Select(n => new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt.ToString("dd/MM HH:mm")))
            .ToList();
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default)
        => db.Notification.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkReadAsync(Guid userId, int id, CancellationToken ct = default)
    {
        var n = await db.Notification.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is null || n.IsRead) return;
        n.IsRead = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await db.Notification.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        if (unread.Count == 0) return;
        foreach (var n in unread) n.IsRead = true;
        await db.SaveChangesAsync(ct);
    }
}
