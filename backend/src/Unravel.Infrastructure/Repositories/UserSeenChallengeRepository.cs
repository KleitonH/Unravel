using Microsoft.EntityFrameworkCore;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Repositories;

/// <summary>
/// PR 37 — implementação Postgres do <see cref="IUserSeenChallengeRepository"/>.
///
/// <para><b>UPSERT</b>: usa raw SQL via Npgsql porque EF Core 8 ainda
/// não tem <c>OnConflictDoUpdate</c> nativo. PostgreSQL é o único provider
/// suportado em produção; pra testes InMemory, o caller injeta uma versão
/// stub.</para>
/// </summary>
public sealed class UserSeenChallengeRepository : IUserSeenChallengeRepository
{
    private readonly ApplicationDbContext _db;

    public UserSeenChallengeRepository(ApplicationDbContext db) => _db = db;

    public async Task MarkAsync(
        Guid userId, int generatedChallengeId, bool wasCorrect,
        DateTime seenAt, CancellationToken ct = default)
    {
        // UPSERT idempotente — Postgres-native ON CONFLICT.
        // Provider InMemory não suporta SQL raw; ramo de fallback usa tracking.
        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO user_seen_challenge (user_id, generated_challenge_id, seen_at, was_correct)
                VALUES ({userId}, {generatedChallengeId}, {seenAt}, {wasCorrect})
                ON CONFLICT (user_id, generated_challenge_id)
                DO UPDATE SET seen_at = EXCLUDED.seen_at, was_correct = EXCLUDED.was_correct;
            ", ct);
            return;
        }

        // Fallback InMemory — leitura + insert/update via tracking.
        var existing = await _db.UserSeenChallenge
            .FirstOrDefaultAsync(s => s.UserId == userId && s.GeneratedChallengeId == generatedChallengeId, ct);
        if (existing is null)
        {
            _db.UserSeenChallenge.Add(new UserSeenChallenge
            {
                UserId               = userId,
                GeneratedChallengeId = generatedChallengeId,
                SeenAt               = seenAt,
                WasCorrect           = wasCorrect,
            });
        }
        else
        {
            existing.SeenAt     = seenAt;
            existing.WasCorrect = wasCorrect;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HashSet<int>> GetSeenIdsAsync(
        Guid userId, IReadOnlyCollection<int> candidateIds, CancellationToken ct = default)
    {
        if (candidateIds.Count == 0) return new HashSet<int>();

        var seen = await _db.UserSeenChallenge
            .AsNoTracking()
            .Where(s => s.UserId == userId && candidateIds.Contains(s.GeneratedChallengeId))
            .Select(s => s.GeneratedChallengeId)
            .ToListAsync(ct);

        return seen.ToHashSet();
    }
}
