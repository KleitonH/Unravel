using Microsoft.EntityFrameworkCore;
using Unravel.Application.Journey.Ports;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Journey;

public sealed class DailyReplanReadModel : IDailyReplanReadModel
{
    private readonly ApplicationDbContext _db;
    public DailyReplanReadModel(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ReplanTarget>> GetActiveTargetsAsync(CancellationToken ct = default)
    {
        // user ativo + UserTrail ativo + Trail ativa. JOIN explícito via LINQ
        // que o Npgsql traduz em JOIN nativo.
        return await (
            from ut in _db.UserTrail.AsNoTracking()
            join u  in _db.User.AsNoTracking()  on ut.UserId  equals u.Id
            join t  in _db.Trail.AsNoTracking() on ut.TrailId equals t.Id
            where ut.IsActive && u.IsActive && t.IsActive
            select new ReplanTarget(ut.UserId, ut.TrailId)
        ).ToListAsync(ct);
    }

    public Task<int> CountUserChallengesSubmittedAsync(
        Guid userId, DateTime fromInclusiveUtc, DateTime toExclusiveUtc, CancellationToken ct = default)
        => _db.UserChallenge
              .AsNoTracking()
              .CountAsync(uc => uc.UserId == userId
                             && uc.IsCompleted
                             && uc.CompletedAt != null
                             && uc.CompletedAt >= fromInclusiveUtc
                             && uc.CompletedAt <  toExclusiveUtc, ct);

    public Task<UserCronSnapshot?> GetUserCronSnapshotAsync(Guid userId, CancellationToken ct = default)
        => _db.User
              .AsNoTracking()
              .Where(u => u.Id == userId)
              .Select(u => new UserCronSnapshot(u.Lives, u.StreakDays, u.LastActivityDate))
              .FirstOrDefaultAsync(ct);

    public Task UpdateUserStreakAsync(Guid userId, int newStreak, CancellationToken ct = default)
        => _db.User
              .Where(u => u.Id == userId)
              .ExecuteUpdateAsync(s => s.SetProperty(u => u.StreakDays, _ => newStreak), ct);
}
