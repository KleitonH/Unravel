using Microsoft.EntityFrameworkCore;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Social;

/// <summary>
/// PR 65c — eventos entre caixinhas. Pontos do evento = pontos coletivos atuais
/// (ΣXP dos membros) − baseline capturado na inscrição. Ao encerrar, o
/// resultado é congelado em FinalPoints (lazy, na primeira leitura pós-fim).
/// </summary>
public class CaixinhaEventService(ApplicationDbContext db) : ICaixinhaEventService
{
    private static string Status(CaixinhaEvent ev, DateTime now) =>
        now < ev.StartsAt ? "upcoming" : now > ev.EndsAt ? "finished" : "active";

    public async Task<CaixinhaEventResult> CreateAsync(Guid creatorUserId, string name, string? theme, DateTime startsAt, DateTime endsAt, CancellationToken ct = default)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length < 2) return new CaixinhaEventResult(CaixinhaEventOutcome.NameTooShort);
        if (endsAt <= startsAt) return new CaixinhaEventResult(CaixinhaEventOutcome.InvalidDates);

        var ev = new CaixinhaEvent
        {
            Name = name,
            Theme = string.IsNullOrWhiteSpace(theme) ? null : theme.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedByUserId = creatorUserId,
            CreatedAt = DateTime.UtcNow,
        };
        db.CaixinhaEvent.Add(ev);
        await db.SaveChangesAsync(ct);
        return new CaixinhaEventResult(CaixinhaEventOutcome.Ok, ev.Id);
    }

    public async Task<IReadOnlyList<CaixinhaEventDto>> ListAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        var events = await db.CaixinhaEvent.AsNoTracking().ToListAsync(ct);
        if (events.Count == 0) return [];

        var myCaixinhaId = await MyCaixinhaIdAsync(userId, ct);

        var scoreRows = await db.CaixinhaEventScore.AsNoTracking()
            .Select(s => new { s.EventId, s.CaixinhaId })
            .ToListAsync(ct);
        var countByEvent = scoreRows.GroupBy(s => s.EventId).ToDictionary(g => g.Key, g => g.Count());
        var joined = myCaixinhaId is null
            ? new HashSet<int>()
            : scoreRows.Where(s => s.CaixinhaId == myCaixinhaId).Select(s => s.EventId).ToHashSet();

        return events
            .Select(ev => new
            {
                Dto = new CaixinhaEventDto(
                    ev.Id, ev.Name, ev.Theme,
                    ev.StartsAt.ToString("dd/MM/yyyy"), ev.EndsAt.ToString("dd/MM/yyyy"),
                    Status(ev, now),
                    countByEvent.GetValueOrDefault(ev.Id, 0),
                    joined.Contains(ev.Id)),
                ev.StartsAt,
            })
            // ativo (0) → em breve (1) → encerrado (2); dentro, por data.
            .OrderBy(x => x.Dto.Status == "active" ? 0 : x.Dto.Status == "upcoming" ? 1 : 2)
            .ThenByDescending(x => x.StartsAt)
            .Select(x => x.Dto)
            .ToList();
    }

    public async Task<CaixinhaEventDetailDto?> GetDetailAsync(Guid userId, int eventId, DateTime now, CancellationToken ct = default)
    {
        var ev = await db.CaixinhaEvent.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return null;

        var scores = await db.CaixinhaEventScore
            .Where(s => s.EventId == eventId)
            .ToListAsync(ct); // tracked: pode congelar FinalPoints

        var collective = await CollectiveAsync(ct);
        var finished = now > ev.EndsAt;

        var changed = false;
        foreach (var s in scores)
        {
            if (finished && s.FinalPoints is null)
            {
                s.FinalPoints = Math.Max(0, collective.GetValueOrDefault(s.CaixinhaId, 0) - s.BaselinePoints);
                changed = true;
            }
        }
        if (changed) await db.SaveChangesAsync(ct);

        var ids = scores.Select(s => s.CaixinhaId).ToList();
        var info = await db.Caixinha.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Emblem })
            .ToListAsync(ct);

        var myCaixinhaId = await MyCaixinhaIdAsync(userId, ct);

        var ranking = scores
            .Select(s =>
            {
                var ci = info.FirstOrDefault(c => c.Id == s.CaixinhaId);
                var pts = s.FinalPoints ?? Math.Max(0, collective.GetValueOrDefault(s.CaixinhaId, 0) - s.BaselinePoints);
                return new EventRankingEntryDto(s.CaixinhaId, ci?.Name ?? "—", ci?.Emblem ?? "📦", pts, 0, s.CaixinhaId == myCaixinhaId);
            })
            .OrderByDescending(r => r.Points)
            .ThenBy(r => r.CaixinhaId)
            .Select((r, i) => r with { Rank = i + 1 })
            .ToList();

        var dto = new CaixinhaEventDto(
            ev.Id, ev.Name, ev.Theme,
            ev.StartsAt.ToString("dd/MM/yyyy"), ev.EndsAt.ToString("dd/MM/yyyy"),
            Status(ev, now), scores.Count,
            myCaixinhaId is not null && scores.Any(s => s.CaixinhaId == myCaixinhaId));

        return new CaixinhaEventDetailDto(dto, ranking);
    }

    public async Task<CaixinhaEventResult> JoinAsync(Guid userId, int eventId, DateTime now, CancellationToken ct = default)
    {
        var me = await db.CaixinhaMember.FirstOrDefaultAsync(m => m.UserId == userId, ct);
        if (me is null) return new CaixinhaEventResult(CaixinhaEventOutcome.NotInAny);
        if (me.Role != CaixinhaRole.Leader) return new CaixinhaEventResult(CaixinhaEventOutcome.NotLeader);

        var ev = await db.CaixinhaEvent.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return new CaixinhaEventResult(CaixinhaEventOutcome.NotFound);
        if (Status(ev, now) != "active") return new CaixinhaEventResult(CaixinhaEventOutcome.NotActive);

        if (await db.CaixinhaEventScore.AnyAsync(s => s.EventId == eventId && s.CaixinhaId == me.CaixinhaId, ct))
            return new CaixinhaEventResult(CaixinhaEventOutcome.AlreadyJoined);

        var collective = await CollectiveAsync(ct);
        db.CaixinhaEventScore.Add(new CaixinhaEventScore
        {
            EventId = eventId,
            CaixinhaId = me.CaixinhaId,
            BaselinePoints = collective.GetValueOrDefault(me.CaixinhaId, 0),
            JoinedAt = now,
        });
        await db.SaveChangesAsync(ct);
        return new CaixinhaEventResult(CaixinhaEventOutcome.Ok, eventId);
    }

    // ── helpers ──────────────────────────────────────────────────────

    private async Task<int?> MyCaixinhaIdAsync(Guid userId, CancellationToken ct) =>
        await db.CaixinhaMember.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => (int?)m.CaixinhaId)
            .FirstOrDefaultAsync(ct);

    /// <summary>caixinhaId → pontos coletivos (ΣXP dos membros).</summary>
    private async Task<Dictionary<int, int>> CollectiveAsync(CancellationToken ct)
    {
        var rows = await db.CaixinhaMember.AsNoTracking()
            .Select(m => new { m.CaixinhaId, Xp = m.User!.Xp })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.CaixinhaId).ToDictionary(g => g.Key, g => g.Sum(x => x.Xp));
    }
}
