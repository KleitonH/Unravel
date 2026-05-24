using Microsoft.EntityFrameworkCore;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Repositories;

/// <summary>
/// Repositório de <see cref="Mastery"/> sobre EF Core + PostgreSQL. As
/// queries-chave (por trilha, por revisão vencida) já têm índices cobrindo
/// — ver <c>MasteryConfiguration</c>.
///
/// <para>"Due for review" é filtrado em memória após carregar a janela
/// candidata, e não via SQL com expressão computada. Justificativa: o
/// volume por usuário×trilha é pequeno (dezenas de tópicos), e isolar a
/// regra "asOf &gt;= LastSeenAt + SrsIntervalDays" no domínio
/// (<see cref="MasteryScoring.IsDueForReview"/>) mantém a fonte da
/// verdade em um único lugar.</para>
/// </summary>
public sealed class MasteryRepository : IMasteryRepository
{
    private readonly ApplicationDbContext _db;

    public MasteryRepository(ApplicationDbContext db) => _db = db;

    public Task<Mastery?> GetAsync(Guid userId, int topicId, CancellationToken ct = default)
        => _db.Mastery.AsTracking()
                      .FirstOrDefaultAsync(m => m.UserId == userId && m.TopicId == topicId, ct);

    public async Task<IReadOnlyList<Mastery>> GetByTrailAsync(
        Guid userId, int trailId, CancellationToken ct = default)
        => await _db.Mastery
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.TrailId == trailId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Mastery>> GetDueForReviewAsync(
        Guid userId, int trailId, DateTime asOf, CancellationToken ct = default)
    {
        // Pré-filtra por uma janela bem ampla (LastSeenAt <= asOf) e aplica
        // a regra exata em memória. Postgres não aceita expressões em índice
        // sem migration custom — não vale a pena pra esse volume.
        var candidates = await _db.Mastery
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.TrailId == trailId && m.LastSeenAt <= asOf)
            .ToListAsync(ct);

        return candidates.Where(m => MasteryScoring.IsDueForReview(m, asOf)).ToList();
    }

    public async Task UpsertAsync(Mastery mastery, CancellationToken ct = default)
    {
        var existing = await _db.Mastery
            .FirstOrDefaultAsync(m => m.UserId == mastery.UserId && m.TopicId == mastery.TopicId, ct);

        if (existing is null)
        {
            _db.Mastery.Add(mastery);
        }
        else
        {
            existing.Score           = mastery.Score;
            existing.Confidence      = mastery.Confidence;
            existing.LastSeenAt      = mastery.LastSeenAt;
            existing.SrsIntervalDays = mastery.SrsIntervalDays;
            existing.EaseFactor      = mastery.EaseFactor;
            existing.TrailId         = mastery.TrailId;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertManyAsync(IEnumerable<Mastery> masteries, CancellationToken ct = default)
    {
        var list = masteries.ToList();
        if (list.Count == 0) return;

        // Carrega num único round-trip todos os que já existem, indexado por chave composta.
        var keys = list.Select(m => new { m.UserId, m.TopicId }).ToList();
        var userIds  = keys.Select(k => k.UserId).Distinct().ToList();
        var topicIds = keys.Select(k => k.TopicId).Distinct().ToList();

        var existing = await _db.Mastery
            .Where(m => userIds.Contains(m.UserId) && topicIds.Contains(m.TopicId))
            .ToListAsync(ct);

        var index = existing.ToDictionary(m => (m.UserId, m.TopicId));

        foreach (var incoming in list)
        {
            if (index.TryGetValue((incoming.UserId, incoming.TopicId), out var current))
            {
                current.Score           = incoming.Score;
                current.Confidence      = incoming.Confidence;
                current.LastSeenAt      = incoming.LastSeenAt;
                current.SrsIntervalDays = incoming.SrsIntervalDays;
                current.EaseFactor      = incoming.EaseFactor;
                current.TrailId         = incoming.TrailId;
            }
            else
            {
                _db.Mastery.Add(incoming);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
