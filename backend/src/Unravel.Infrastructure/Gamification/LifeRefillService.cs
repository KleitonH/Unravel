using Microsoft.EntityFrameworkCore;
using Unravel.Application.Gamification.Ports;
using Unravel.Domain.Gamification;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Gamification;

/// <summary>
/// UC34 — implementação EF da recarga de vidas. Toda a regra mora em
/// <see cref="LifeRefill.Compute"/> (pura/testável); aqui só carregamos,
/// aplicamos e persistimos.
/// </summary>
public sealed class LifeRefillService(ApplicationDbContext db) : ILifeRefillService
{
    public async Task<int> RefillUserAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        var user = await db.User.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return 0;

        var r = LifeRefill.Compute(user.Lives, user.LastLifeRefillAt, nowUtc);
        var changed = r.Lives != user.Lives || r.Anchor != user.LastLifeRefillAt;
        if (!changed) return 0;

        user.Lives = r.Lives;
        user.LastLifeRefillAt = r.Anchor;
        await db.SaveChangesAsync(ct);
        return r.Granted;
    }

    public async Task<int> RefillAllAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        // Só quem está abaixo do teto pode ganhar vida. Quem está no teto não
        // precisa de write (a âncora dele só importa quando cair abaixo).
        var users = await db.User
            .Where(u => u.IsActive && u.Lives < LifeRefill.MaxLives)
            .ToListAsync(ct);

        var affected = 0;
        foreach (var user in users)
        {
            var r = LifeRefill.Compute(user.Lives, user.LastLifeRefillAt, nowUtc);
            if (r.Lives == user.Lives && r.Anchor == user.LastLifeRefillAt) continue;

            user.Lives = r.Lives;
            user.LastLifeRefillAt = r.Anchor;
            if (r.Granted > 0) affected++;
        }

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
        return affected;
    }
}
