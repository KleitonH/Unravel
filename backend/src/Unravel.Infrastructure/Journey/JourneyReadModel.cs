using Microsoft.EntityFrameworkCore;
using Unravel.Application.Journey.Ports;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Journey;

/// <summary>
/// Implementação EF de <see cref="IJourneyReadModel"/>. Cada método é
/// uma única query, sem tracking — leituras de relatório/planejamento
/// não modificam estado.
/// </summary>
public sealed class JourneyReadModel : IJourneyReadModel
{
    private readonly ApplicationDbContext _db;
    public JourneyReadModel(ApplicationDbContext db) => _db = db;

    public Task<TrailMeta?> GetTrailMetaAsync(int trailId, CancellationToken ct = default)
        => _db.Trail
              .AsNoTracking()
              .Where(t => t.Id == trailId && t.IsActive)
              .Select(t => new TrailMeta(t.Id, t.Name))
              .FirstOrDefaultAsync(ct);

    public Task<UserJourneyState?> GetUserStateAsync(Guid userId, CancellationToken ct = default)
        => _db.User
              .AsNoTracking()
              .Where(u => u.Id == userId)
              .Select(u => new UserJourneyState(u.Lives, u.StreakDays))
              .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<int, string>> GetContentTitlesAsync(
        IReadOnlyCollection<int> contentIds, CancellationToken ct = default)
    {
        if (contentIds.Count == 0) return new Dictionary<int, string>();

        return await _db.Content
            .AsNoTracking()
            .Where(c => contentIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title, ct);
    }

    public Task<int> CountChallengesAnsweredAsync(
        Guid userId, int trailId, DateTime fromInclusiveUtc, DateTime toExclusiveUtc,
        CancellationToken ct = default)
        // join UserSeenChallenge (gravado no submit) ↔ GeneratedChallenge.TrailId.
        => (from seen in _db.UserSeenChallenge.AsNoTracking()
            join gc in _db.GeneratedChallenge.AsNoTracking() on seen.GeneratedChallengeId equals gc.Id
            where seen.UserId == userId
               && gc.TrailId == trailId
               && seen.SeenAt >= fromInclusiveUtc
               && seen.SeenAt <  toExclusiveUtc
            select seen.GeneratedChallengeId)
           .CountAsync(ct);

    public async Task<TodayGoal?> GetTodayGoalAsync(
        Guid userId, int trailId, DateTime today, CancellationToken ct = default)
        => await _db.JourneySnapshot
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.TrailId == trailId && s.PlanDate == today.Date)
            .Select(s => new TodayGoal(s.MetaDia, s.ExtraChallengesPenalty))
            .FirstOrDefaultAsync(ct);
}
