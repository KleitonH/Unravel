using Microsoft.EntityFrameworkCore;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Repositories;

public sealed class JourneySnapshotRepository : IJourneySnapshotRepository
{
    private readonly ApplicationDbContext _db;
    public JourneySnapshotRepository(ApplicationDbContext db) => _db = db;

    public Task<JourneySnapshot?> GetByUserTrailDateAsync(
        Guid userId, int trailId, DateTime planDate, CancellationToken ct = default)
        => _db.JourneySnapshot
              .AsNoTracking()
              .FirstOrDefaultAsync(s => s.UserId == userId
                                     && s.TrailId == trailId
                                     && s.PlanDate == planDate, ct);

    public async Task UpsertAsync(JourneySnapshot snapshot, CancellationToken ct = default)
    {
        var existing = await _db.JourneySnapshot
            .FirstOrDefaultAsync(s => s.UserId == snapshot.UserId
                                   && s.TrailId == snapshot.TrailId
                                   && s.PlanDate == snapshot.PlanDate, ct);

        if (existing is null)
        {
            _db.JourneySnapshot.Add(snapshot);
        }
        else
        {
            existing.MetaDia                = snapshot.MetaDia;
            existing.ExtraChallengesPenalty = snapshot.ExtraChallengesPenalty;
            existing.PlanJson               = snapshot.PlanJson;
            existing.GeneratedAt            = snapshot.GeneratedAt;
            // MetGoal preservado se já avaliado.
            if (snapshot.MetGoal is not null) existing.MetGoal = snapshot.MetGoal;
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task MarkGoalAsync(
        Guid userId, int trailId, DateTime planDate, bool met, CancellationToken ct = default)
        => _db.JourneySnapshot
              .Where(s => s.UserId == userId && s.TrailId == trailId && s.PlanDate == planDate)
              .ExecuteUpdateAsync(s => s.SetProperty(x => x.MetGoal, _ => met), ct);
}
