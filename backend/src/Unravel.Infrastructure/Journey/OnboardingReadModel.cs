using Microsoft.EntityFrameworkCore;
using Unravel.Application.Journey.Onboarding;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Journey;

/// <summary>Implementação EF de <see cref="IOnboardingReadModel"/>.
/// Operações granulares ao caso de uso (mesma motivação do
/// <c>IJourneyReadModel</c>): port reflete intenção do consumidor.</summary>
public sealed class OnboardingReadModel : IOnboardingReadModel
{
    private readonly ApplicationDbContext _db;
    public OnboardingReadModel(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TrailMeta>> GetTrailsByIdsAsync(
        IReadOnlyCollection<int> trailIds, CancellationToken ct = default)
    {
        if (trailIds.Count == 0) return Array.Empty<TrailMeta>();
        return await _db.Trail
            .AsNoTracking()
            .Where(t => trailIds.Contains(t.Id) && t.IsActive)
            .OrderBy(t => t.Id)
            .Select(t => new TrailMeta(t.Id, t.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<Content>>> GetContentsForTrailsAsync(
        IReadOnlyCollection<int> trailIds, CancellationToken ct = default)
    {
        if (trailIds.Count == 0)
            return new Dictionary<int, IReadOnlyList<Content>>();

        var contents = await _db.Content
            .AsNoTracking()
            .Where(c => trailIds.Contains(c.TrailId) && c.IsActive)
            .ToListAsync(ct);

        return contents
            .GroupBy(c => c.TrailId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Content>)g.OrderBy(c => c.Order).ToList());
    }

    public Task<bool> UserHasAnyMasteryAsync(
        Guid userId, IReadOnlyCollection<int> trailIds, CancellationToken ct = default)
        => _db.Mastery
              .AsNoTracking()
              .AnyAsync(m => m.UserId == userId && trailIds.Contains(m.TrailId), ct);
}
