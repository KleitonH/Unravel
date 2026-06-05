using Microsoft.EntityFrameworkCore;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Repositories;

public sealed class GeneratedChallengeRepository : IGeneratedChallengeRepository
{
    private readonly ApplicationDbContext _db;

    public GeneratedChallengeRepository(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<GeneratedChallenge>> GetByContentAsync(
        int contentId, CancellationToken ct = default)
        => await _db.GeneratedChallenge
            .AsNoTracking()
            .Where(g => g.ContentId == contentId && g.IsActive)
            .OrderBy(g => g.ServedCount)
            .ThenBy(g => g.Id)
            .ToListAsync(ct);

    public async Task AddManyAsync(IEnumerable<GeneratedChallenge> drafts, CancellationToken ct = default)
    {
        var list = drafts.ToList();
        if (list.Count == 0) return;
        _db.GeneratedChallenge.AddRange(list);
        await _db.SaveChangesAsync(ct);
    }

    public async Task IncrementServedAsync(IEnumerable<int> challengeIds, CancellationToken ct = default)
    {
        var ids = challengeIds.ToList();
        if (ids.Count == 0) return;

        // ExecuteUpdate (EF Core 7+) — single UPDATE, sem materializar.
        await _db.GeneratedChallenge
            .Where(g => ids.Contains(g.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ServedCount, g => g.ServedCount + 1), ct);
    }

    public Task<GeneratedChallenge?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.GeneratedChallenge
              .AsNoTracking()
              .FirstOrDefaultAsync(g => g.Id == id && g.IsActive, ct);

    public async Task RecordOutcomeAsync(int challengeId, bool correct, CancellationToken ct = default)
    {
        // Média móvel "online" — atualiza CorrectRate sem materializar:
        //   newRate = (oldRate * served + (correct ? 1 : 0)) / (served + 1)
        // Em seguida, served += 1. Tudo num único UPDATE — atômico contra
        // múltiplas respostas concorrentes pra mesma pergunta.
        var outcome = correct ? 1.0 : 0.0;
        await _db.GeneratedChallenge
            .Where(g => g.Id == challengeId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.CorrectRate,
                    g => (g.CorrectRate * g.ServedCount + outcome) / (g.ServedCount + 1))
                .SetProperty(g => g.ServedCount, g => g.ServedCount + 1), ct);
    }

    public async Task<IReadOnlyList<GeneratedChallenge>> GetByTrailAndTopicsAsync(
        int trailId, IReadOnlyCollection<int> topicIds, CancellationToken ct = default)
    {
        if (topicIds.Count == 0) return Array.Empty<GeneratedChallenge>();
        return await _db.GeneratedChallenge
            .AsNoTracking()
            .Where(g => g.TrailId == trailId
                        && g.IsActive
                        && topicIds.Contains(g.TopicId))
            .OrderBy(g => g.ServedCount)
            .ThenBy(g => g.Id)
            .ToListAsync(ct);
    }
}
