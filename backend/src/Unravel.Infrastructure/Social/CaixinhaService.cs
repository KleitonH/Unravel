using Microsoft.EntityFrameworkCore;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Social;

/// <summary>
/// PR 65 — Caixinha de Gatos (clã/grupo). Placar coletivo derivado da soma de
/// XP dos membros (sem write-path próprio nesta fatia). Acessa DbContext
/// direto (padrão FriendshipService/CosmeticShopService).
/// </summary>
public class CaixinhaService(ApplicationDbContext db) : ICaixinhaService
{
    public async Task<CaixinhaDetailDto?> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var me = await db.CaixinhaMember.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);
        if (me is null) return null;

        var caixinha = await db.Caixinha.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == me.CaixinhaId, ct);
        if (caixinha is null) return null;

        var today = DateTime.UtcNow.Date;

        var members = await db.CaixinhaMember.AsNoTracking()
            .Where(m => m.CaixinhaId == me.CaixinhaId)
            .Select(m => new
            {
                m.UserId, m.Role, m.JoinedAt,
                m.User!.Name, m.User.Xp, m.User.StreakDays, m.User.LastActivityDate,
            })
            .ToListAsync(ct);

        var memberDtos = members
            .OrderByDescending(m => m.Xp)
            .Select(m => new CaixinhaMemberDto(
                m.UserId, m.Name, m.Xp, m.StreakDays,
                m.LastActivityDate.HasValue && m.LastActivityDate.Value.Date == today,
                m.Role.ToString()))
            .ToList();

        var mural = await db.CaixinhaMessage.AsNoTracking()
            .Where(msg => msg.CaixinhaId == me.CaixinhaId)
            .OrderByDescending(msg => msg.CreatedAt)
            .Take(30)
            .Select(msg => new { msg.Id, msg.UserId, msg.User!.Name, msg.Text, msg.CreatedAt })
            .ToListAsync(ct);

        var muralDtos = mural
            .Select(m => new CaixinhaMessageDto(m.Id, m.UserId, m.Name, m.Text, m.CreatedAt.ToString("dd/MM HH:mm")))
            .ToList();

        var ranked = await RankedAsync(ct);
        var rank   = ranked.FirstOrDefault(r => r.Id == me.CaixinhaId)?.Rank ?? 0;
        var points = memberDtos.Sum(m => m.Xp);

        var dailyPoints  = caixinha.DailyPointsDate?.Date == today ? caixinha.DailyPoints : 0;
        var goalReached  = caixinha.DailyGoalAwardedAt?.Date == today;

        return new CaixinhaDetailDto(
            caixinha.Id, caixinha.Name, caixinha.Emblem, caixinha.LeaderId,
            points, memberDtos.Count, memberDtos.Count(m => m.ActiveToday), rank,
            me.Role.ToString(),
            caixinha.DailyGoal, dailyPoints, goalReached,
            memberDtos, muralDtos);
    }

    public async Task<CaixinhaActionResult> CreateAsync(Guid userId, string name, string? emblem, CancellationToken ct = default)
    {
        if (await db.CaixinhaMember.AnyAsync(m => m.UserId == userId, ct))
            return new CaixinhaActionResult(CaixinhaOutcome.AlreadyInOne);

        name = (name ?? string.Empty).Trim();
        if (name.Length < 2)
            return new CaixinhaActionResult(CaixinhaOutcome.NameTooShort);

        var caixinha = new Caixinha
        {
            Name     = name,
            Emblem   = string.IsNullOrWhiteSpace(emblem) ? "📦" : emblem.Trim(),
            LeaderId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Caixinha.Add(caixinha);
        await db.SaveChangesAsync(ct);

        db.CaixinhaMember.Add(new CaixinhaMember
        {
            CaixinhaId = caixinha.Id,
            UserId     = userId,
            Role       = CaixinhaRole.Leader,
            JoinedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return new CaixinhaActionResult(CaixinhaOutcome.Ok, caixinha.Id);
    }

    public async Task<IReadOnlyList<CaixinhaSummaryDto>> BrowseAsync(string? query, int take, CancellationToken ct = default)
    {
        var ranked = await RankedAsync(ct);
        query = (query ?? string.Empty).Trim().ToLower();
        if (query.Length >= 1)
            ranked = ranked.Where(c => c.Name.ToLower().Contains(query)).ToList();
        return ranked.Take(take <= 0 ? 20 : Math.Min(take, 50)).ToList();
    }

    public async Task<IReadOnlyList<CaixinhaSummaryDto>> LeaderboardAsync(int top, CancellationToken ct = default)
    {
        var ranked = await RankedAsync(ct);
        return ranked.Take(top <= 0 ? 10 : Math.Min(top, 100)).ToList();
    }

    public async Task<CaixinhaActionResult> JoinAsync(Guid userId, int caixinhaId, CancellationToken ct = default)
    {
        if (await db.CaixinhaMember.AnyAsync(m => m.UserId == userId, ct))
            return new CaixinhaActionResult(CaixinhaOutcome.AlreadyInOne);

        var exists = await db.Caixinha.AnyAsync(c => c.Id == caixinhaId, ct);
        if (!exists) return new CaixinhaActionResult(CaixinhaOutcome.NotFound);

        var count = await db.CaixinhaMember.CountAsync(m => m.CaixinhaId == caixinhaId, ct);
        if (count >= Caixinha.MaxMembers)
            return new CaixinhaActionResult(CaixinhaOutcome.Full);

        db.CaixinhaMember.Add(new CaixinhaMember
        {
            CaixinhaId = caixinhaId,
            UserId     = userId,
            Role       = CaixinhaRole.Member,
            JoinedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return new CaixinhaActionResult(CaixinhaOutcome.Ok, caixinhaId);
    }

    public async Task<CaixinhaActionResult> LeaveAsync(Guid userId, CancellationToken ct = default)
    {
        var me = await db.CaixinhaMember.FirstOrDefaultAsync(m => m.UserId == userId, ct);
        if (me is null) return new CaixinhaActionResult(CaixinhaOutcome.NotInAny);

        var others = await db.CaixinhaMember
            .Where(m => m.CaixinhaId == me.CaixinhaId && m.UserId != userId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(ct);

        if (me.Role == CaixinhaRole.Leader && others.Count == 0)
        {
            // Último membro (líder sozinho) → dissolve a caixinha.
            var caixinha = await db.Caixinha.FirstOrDefaultAsync(c => c.Id == me.CaixinhaId, ct);
            db.CaixinhaMember.Remove(me);
            if (caixinha is not null) db.Caixinha.Remove(caixinha); // cascata remove mural
            await db.SaveChangesAsync(ct);
            return new CaixinhaActionResult(CaixinhaOutcome.Disbanded, me.CaixinhaId);
        }

        if (me.Role == CaixinhaRole.Leader)
        {
            // Transfere a liderança pro membro mais antigo.
            var next = others[0];
            next.Role = CaixinhaRole.Leader;
            var caixinha = await db.Caixinha.FirstOrDefaultAsync(c => c.Id == me.CaixinhaId, ct);
            if (caixinha is not null) caixinha.LeaderId = next.UserId;
        }

        db.CaixinhaMember.Remove(me);
        await db.SaveChangesAsync(ct);
        return new CaixinhaActionResult(CaixinhaOutcome.Ok, me.CaixinhaId);
    }

    public async Task<CaixinhaActionResult> KickAsync(Guid leaderId, Guid targetUserId, CancellationToken ct = default)
    {
        if (leaderId == targetUserId)
            return new CaixinhaActionResult(CaixinhaOutcome.NotMember); // líder usa "sair"

        var leader = await db.CaixinhaMember.FirstOrDefaultAsync(m => m.UserId == leaderId, ct);
        if (leader is null || leader.Role != CaixinhaRole.Leader)
            return new CaixinhaActionResult(CaixinhaOutcome.NotLeader);

        var target = await db.CaixinhaMember
            .FirstOrDefaultAsync(m => m.UserId == targetUserId && m.CaixinhaId == leader.CaixinhaId, ct);
        if (target is null) return new CaixinhaActionResult(CaixinhaOutcome.NotMember);

        db.CaixinhaMember.Remove(target);
        await db.SaveChangesAsync(ct);
        return new CaixinhaActionResult(CaixinhaOutcome.Ok, leader.CaixinhaId);
    }

    public async Task<CaixinhaActionResult> PostMessageAsync(Guid userId, string text, CancellationToken ct = default)
    {
        var me = await db.CaixinhaMember.FirstOrDefaultAsync(m => m.UserId == userId, ct);
        if (me is null) return new CaixinhaActionResult(CaixinhaOutcome.NotInAny);

        text = (text ?? string.Empty).Trim();
        if (text.Length == 0) return new CaixinhaActionResult(CaixinhaOutcome.EmptyMessage);
        if (text.Length > 500) text = text[..500];

        db.CaixinhaMessage.Add(new CaixinhaMessage
        {
            CaixinhaId = me.CaixinhaId,
            UserId     = userId,
            Text       = text,
            CreatedAt  = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return new CaixinhaActionResult(CaixinhaOutcome.Ok, me.CaixinhaId);
    }

    /// <summary>Todas as caixinhas com pontos coletivos (soma de XP) e rank.</summary>
    private async Task<List<CaixinhaSummaryDto>> RankedAsync(CancellationToken ct)
    {
        var caixinhas = await db.Caixinha.AsNoTracking()
            .Select(c => new { c.Id, c.Name, c.Emblem })
            .ToListAsync(ct);

        var rows = await db.CaixinhaMember.AsNoTracking()
            .Select(m => new { m.CaixinhaId, Xp = m.User!.Xp })
            .ToListAsync(ct);

        var byId = rows
            .GroupBy(r => r.CaixinhaId)
            .ToDictionary(g => g.Key, g => (Points: g.Sum(x => x.Xp), Count: g.Count()));

        return caixinhas
            .Select(c =>
            {
                var agg = byId.TryGetValue(c.Id, out var v) ? v : (Points: 0, Count: 0);
                return new CaixinhaSummaryDto(c.Id, c.Name, c.Emblem, agg.Count, agg.Points, 0);
            })
            .OrderByDescending(s => s.CollectivePoints)
            .ThenBy(s => s.Id)
            .Select((s, i) => s with { Rank = i + 1 })
            .ToList();
    }
}
