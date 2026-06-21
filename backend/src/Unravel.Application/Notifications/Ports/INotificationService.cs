using Unravel.Domain.Entities;

namespace Unravel.Application.Notifications.Ports;

/// <summary>PR 69 — central de notificações in-app.</summary>
public record NotificationDto(
    int     Id,
    string  Type,
    string  Title,
    string  Body,
    string? Link,
    bool    IsRead,
    string  CreatedAt);

public interface INotificationService
{
    Task CreateAsync(Guid userId, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default);
    Task CreateManyAsync(IEnumerable<Guid> userIds, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, int take, CancellationToken ct = default);
    Task<int>  UnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task MarkReadAsync(Guid userId, int id, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
