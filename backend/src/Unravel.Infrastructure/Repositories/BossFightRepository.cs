using Microsoft.EntityFrameworkCore;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Repositories;

/// <summary>
/// PR 50 — implementação Postgres do <see cref="IBossFightRepository"/>.
/// </summary>
public sealed class BossFightRepository : IBossFightRepository
{
    private readonly ApplicationDbContext _db;

    public BossFightRepository(ApplicationDbContext db) => _db = db;

    public Task<BossFightTrailMeta?> GetTrailMetaAsync(int trailId, CancellationToken ct = default)
        => _db.Trail
            .Where(t => t.Id == trailId && t.IsActive)
            .Select(t => new BossFightTrailMeta(t.Id, t.Name))
            .FirstOrDefaultAsync(ct);

    public async Task<int> GetIncompleteContentsCountAsync(
        Guid userId, int trailId, CancellationToken ct = default)
    {
        var totalActive = await _db.Content
            .CountAsync(c => c.TrailId == trailId && c.IsActive, ct);
        if (totalActive == 0) return 0;

        var completed = await _db.UserContent
            .CountAsync(uc => uc.UserId == userId
                           && uc.Content.TrailId == trailId
                           && uc.Status == UserContentStatus.Completed, ct);

        return Math.Max(0, totalActive - completed);
    }

    public async Task<IReadOnlyList<GeneratedChallenge>> GetTrailPoolAsync(
        int trailId, CancellationToken ct = default)
        => await _db.GeneratedChallenge
            .AsNoTracking()
            .Where(g => g.TrailId == trailId && g.IsActive)
            .ToListAsync(ct);

    public Task<UserBossFight?> GetUserBossFightAsync(
        Guid userId, int trailId, CancellationToken ct = default)
        => _db.UserBossFight
            .FirstOrDefaultAsync(b => b.UserId == userId && b.TrailId == trailId, ct);

    public async Task UpsertUserBossFightAsync(UserBossFight record, CancellationToken ct = default)
    {
        var existing = await _db.UserBossFight
            .FirstOrDefaultAsync(b => b.UserId == record.UserId && b.TrailId == record.TrailId, ct);
        if (existing is null)
        {
            _db.UserBossFight.Add(record);
        }
        else
        {
            existing.AttemptCount  = record.AttemptCount;
            existing.BestScore     = record.BestScore;
            existing.LastScore     = record.LastScore;
            existing.LastAttemptAt = record.LastAttemptAt;
            existing.FirstWonAt    = record.FirstWonAt;
        }
        await _db.SaveChangesAsync(ct);
    }
}
