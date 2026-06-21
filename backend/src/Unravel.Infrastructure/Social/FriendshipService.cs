using Microsoft.EntityFrameworkCore;
using Unravel.Application.Notifications.Ports;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Social;

/// <summary>
/// PR 64 — serviço de amizades (grafo social). Acessa o DbContext direto
/// (mesmo padrão de CosmeticShopService/ContentChaptersService). O par é
/// tratado como simétrico: toda checagem olha as duas direções.
/// </summary>
public class FriendshipService(ApplicationDbContext db, INotificationService notifications) : IFriendshipService
{
    public async Task<IReadOnlyList<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken ct = default)
    {
        var links = await db.Friendship
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted
                     && (f.RequesterId == userId || f.AddresseeId == userId))
            .Select(f => new
            {
                f.Id,
                Other = f.RequesterId == userId ? f.Addressee! : f.Requester!,
            })
            .Select(x => new FriendDto(
                x.Other.Id,
                x.Other.Name,
                x.Other.Xp,
                x.Other.StreakDays,
                x.Other.ActiveTitle,
                x.Other.Badges.Count,
                x.Id))
            .ToListAsync(ct);

        return links.OrderByDescending(f => f.Xp).ToList();
    }

    public async Task<FriendRequestsDto> GetRequestsAsync(Guid userId, CancellationToken ct = default)
    {
        var pending = await db.Friendship
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending
                     && (f.AddresseeId == userId || f.RequesterId == userId))
            .Select(f => new
            {
                f.Id,
                f.CreatedAt,
                Incoming    = f.AddresseeId == userId,
                OtherId     = f.AddresseeId == userId ? f.Requester!.Id         : f.Addressee!.Id,
                OtherName   = f.AddresseeId == userId ? f.Requester!.Name        : f.Addressee!.Name,
                OtherXp     = f.AddresseeId == userId ? f.Requester!.Xp          : f.Addressee!.Xp,
                OtherTitle  = f.AddresseeId == userId ? f.Requester!.ActiveTitle : f.Addressee!.ActiveTitle,
            })
            .ToListAsync(ct);

        var mapped = pending.Select(p => new FriendRequestDto(
            p.Id, p.OtherId, p.OtherName, p.OtherXp, p.OtherTitle,
            p.CreatedAt.ToString("dd/MM/yyyy"),
            p.Incoming ? "incoming" : "outgoing")).ToList();

        var incoming = mapped.Where(p => p.Direction == "incoming").ToList();
        var outgoing = mapped.Where(p => p.Direction == "outgoing").ToList();
        return new FriendRequestsDto(incoming, outgoing);
    }

    public async Task<IReadOnlyList<UserSearchDto>> SearchAsync(Guid userId, string query, int take, CancellationToken ct = default)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length < 2) return [];

        var lower = query.ToLower();

        // Vínculos do solicitante (pra anotar relação) — uma query.
        var links = await db.Friendship
            .AsNoTracking()
            .Where(f => f.RequesterId == userId || f.AddresseeId == userId)
            .Select(f => new { f.RequesterId, f.AddresseeId, f.Status })
            .ToListAsync(ct);

        var matches = await db.User
            .AsNoTracking()
            .Where(u => u.Id != userId
                     && u.IsActive
                     && u.Role == Role.Student
                     && u.Name.ToLower().Contains(lower))
            .OrderByDescending(u => u.Xp)
            .Take(take <= 0 ? 20 : Math.Min(take, 50))
            .Select(u => new { u.Id, u.Name, u.Xp, u.ActiveTitle })
            .ToListAsync(ct);

        string Relation(Guid otherId)
        {
            var link = links.FirstOrDefault(l =>
                (l.RequesterId == userId && l.AddresseeId == otherId) ||
                (l.AddresseeId == userId && l.RequesterId == otherId));
            if (link is null) return "none";
            return link.Status switch
            {
                FriendshipStatus.Accepted => "friends",
                FriendshipStatus.Blocked  => "blocked",
                _ => link.RequesterId == userId ? "pending_out" : "pending_in",
            };
        }

        return matches
            .Select(u => new UserSearchDto(u.Id, u.Name, u.Xp, u.ActiveTitle, Relation(u.Id)))
            .ToList();
    }

    public async Task<FriendActionResult> SendRequestAsync(Guid requesterId, Guid addresseeId, CancellationToken ct = default)
    {
        if (requesterId == addresseeId)
            return new FriendActionResult(FriendActionOutcome.CannotSelf);

        var addressee = await db.User.FirstOrDefaultAsync(u => u.Id == addresseeId && u.IsActive, ct);
        if (addressee is null)
            return new FriendActionResult(FriendActionOutcome.NotFound);

        var existing = await db.Friendship.FirstOrDefaultAsync(f =>
            (f.RequesterId == requesterId && f.AddresseeId == addresseeId) ||
            (f.RequesterId == addresseeId && f.AddresseeId == requesterId), ct);

        if (existing is not null)
        {
            return existing.Status switch
            {
                FriendshipStatus.Accepted => new FriendActionResult(FriendActionOutcome.AlreadyFriends, existing.Id),
                FriendshipStatus.Blocked  => new FriendActionResult(FriendActionOutcome.Blocked, existing.Id),
                _ => new FriendActionResult(FriendActionOutcome.AlreadyPending, existing.Id),
            };
        }

        var friendship = new Friendship
        {
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status      = FriendshipStatus.Pending,
            CreatedAt   = DateTime.UtcNow,
        };
        db.Friendship.Add(friendship);
        await db.SaveChangesAsync(ct);

        var requesterName = await db.User.AsNoTracking().Where(u => u.Id == requesterId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        await NotifySafe(addresseeId, NotificationType.FriendRequest, "Novo pedido de amizade",
            $"{requesterName} quer ser seu amigo.", "/amigos", ct);

        return new FriendActionResult(FriendActionOutcome.Ok, friendship.Id);
    }

    public async Task<FriendActionResult> RespondAsync(Guid userId, int friendshipId, bool accept, CancellationToken ct = default)
    {
        var friendship = await db.Friendship.FirstOrDefaultAsync(f => f.Id == friendshipId, ct);
        if (friendship is null)
            return new FriendActionResult(FriendActionOutcome.NotFound);

        // Só o destinatário responde, e só enquanto pendente.
        if (friendship.AddresseeId != userId || friendship.Status != FriendshipStatus.Pending)
            return new FriendActionResult(FriendActionOutcome.NotAuthorized);

        if (accept)
        {
            friendship.Status      = FriendshipStatus.Accepted;
            friendship.RespondedAt = DateTime.UtcNow;
        }
        else
        {
            // Recusar remove o registro → permite novo pedido depois.
            db.Friendship.Remove(friendship);
        }
        await db.SaveChangesAsync(ct);

        if (accept)
        {
            var responderName = await db.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
            await NotifySafe(friendship.RequesterId, NotificationType.FriendAccepted, "Pedido aceito!",
                $"{responderName} aceitou seu pedido de amizade.", "/amigos", ct);
        }

        return new FriendActionResult(FriendActionOutcome.Ok, friendship.Id);
    }

    public async Task<FriendActionResult> RemoveAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
    {
        var friendship = await db.Friendship.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.AddresseeId == otherUserId) ||
            (f.RequesterId == otherUserId && f.AddresseeId == userId), ct);

        if (friendship is null)
            return new FriendActionResult(FriendActionOutcome.NotFound);

        db.Friendship.Remove(friendship);
        await db.SaveChangesAsync(ct);
        return new FriendActionResult(FriendActionOutcome.Ok);
    }

    /// <summary>Cria notificação sem deixar uma falha derrubar a ação social.</summary>
    private async Task NotifySafe(Guid userId, NotificationType type, string title, string body, string? link, CancellationToken ct)
    {
        try { await notifications.CreateAsync(userId, type, title, body, link, ct); }
        catch { /* best-effort */ }
    }
}
