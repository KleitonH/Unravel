using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.Notifications.Ports;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Social;

/// <summary>
/// PR 66 — ligas semanais. XP da semana = User.Xp − BaselineXp (capturado na
/// segunda). Ao virar a semana, o grupo (tier+semana) é finalizado de uma vez
/// na primeira leitura: top sobem, fundo descem, baseline reseta. Promoção só
/// com XP semanal &gt; 0 (evita subir por inatividade).
///
/// Deferido (cron/escala): salas de N=30 por tier; rollover atômico agendado.
/// </summary>
public class LeagueService(ApplicationDbContext db, INotificationService notifications) : ILeagueService
{
    private const int PromoteCount  = 5;
    private const int RelegateCount = 5;

    public async Task<MyLeagueDto> GetMyLeagueAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        var week = WeekKey(now);
        var row = await EnsureCurrentWeekAsync(userId, now, week, ct);

        var cohort = await db.UserLeague
            .AsNoTracking()
            .Where(l => l.Tier == row.Tier && l.WeekKey == week)
            .Select(l => new { l.UserId, l.User!.Name, Weekly = l.User.Xp - l.BaselineXp })
            .ToListAsync(ct);

        var standings = cohort
            .Select(m => new { m.UserId, m.Name, Weekly = Math.Max(0, m.Weekly) })
            .OrderByDescending(m => m.Weekly)
            .ThenBy(m => m.Name)
            .Select((m, i) => new LeagueMemberDto(m.UserId, m.Name, m.Weekly, i + 1, m.UserId == userId))
            .ToList();

        var me   = standings.First(s => s.IsMine);
        var size = standings.Count;

        var promoteZone  = row.Tier < LeagueTier.Mestre ? PromoteCount : 0;
        var relegateZone = row.Tier > LeagueTier.Bronze && size > PromoteCount + RelegateCount ? RelegateCount : 0;

        return new MyLeagueDto(
            row.Tier.ToString(),
            row.Tier < LeagueTier.Mestre ? (row.Tier + 1).ToString() : null,
            row.Tier > LeagueTier.Bronze ? (row.Tier - 1).ToString() : null,
            me.WeeklyXp, me.Rank, size,
            promoteZone, relegateZone,
            row.PreviousResult, row.PreviousRank,
            WeekEnd(week).ToString("dd/MM/yyyy"),
            standings);
    }

    /// <summary>Garante que o aluno está na semana corrente; finaliza a semana
    /// anterior do grupo se necessário. Retorna o row atualizado.</summary>
    private async Task<UserLeague> EnsureCurrentWeekAsync(Guid userId, DateTime now, string week, CancellationToken ct)
    {
        var row = await db.UserLeague.FirstOrDefaultAsync(l => l.UserId == userId, ct);

        if (row is null)
        {
            var xp = await db.User.Where(u => u.Id == userId).Select(u => u.Xp).FirstAsync(ct);
            row = new UserLeague { UserId = userId, Tier = LeagueTier.Bronze, WeekKey = week, BaselineXp = xp, UpdatedAt = now };
            db.UserLeague.Add(row);
            await db.SaveChangesAsync(ct);
            return row;
        }

        if (row.WeekKey == week) return row;

        await FinalizeCohortAsync(row.Tier, row.WeekKey, week, now, ct);

        // recarrega já rolado
        return await db.UserLeague.FirstAsync(l => l.UserId == userId, ct);
    }

    /// <summary>Finaliza todos do grupo (tier, semana antiga) de uma vez:
    /// ranqueia por XP da semana, promove o topo, rebaixa o fundo, reseta.</summary>
    private async Task FinalizeCohortAsync(LeagueTier tier, string oldWeek, string newWeek, DateTime now, CancellationToken ct)
    {
        var members = await db.UserLeague
            .Include(l => l.User)
            .Where(l => l.Tier == tier && l.WeekKey == oldWeek)
            .ToListAsync(ct);
        if (members.Count == 0) return;

        var ranked = members
            .Select(m => new { Row = m, Weekly = Math.Max(0, (m.User?.Xp ?? 0) - m.BaselineXp) })
            .OrderByDescending(x => x.Weekly)
            .ThenBy(x => x.Row.UserId)
            .ToList();

        var n = ranked.Count;
        var canRelegate = n > PromoteCount + RelegateCount;

        var promoted = new List<(Guid UserId, LeagueTier NewTier)>();
        var relegated = new List<(Guid UserId, LeagueTier NewTier)>();

        for (var i = 0; i < n; i++)
        {
            var rank = i + 1;
            var m = ranked[i].Row;
            var weekly = ranked[i].Weekly;
            var result = "stayed";
            var newTier = m.Tier;

            if (rank <= PromoteCount && tier < LeagueTier.Mestre && weekly > 0)
            {
                newTier = tier + 1; result = "promoted";
                promoted.Add((m.UserId, newTier));
            }
            else if (canRelegate && rank > n - RelegateCount && tier > LeagueTier.Bronze)
            {
                newTier = tier - 1; result = "relegated";
                relegated.Add((m.UserId, newTier));
            }

            m.Tier           = newTier;
            m.PreviousRank   = rank;
            m.PreviousResult = result;
            m.WeekKey        = newWeek;
            m.BaselineXp     = m.User?.Xp ?? 0;
            m.UpdatedAt      = now;
        }

        await db.SaveChangesAsync(ct);

        // Notifica resultado da semana (best-effort).
        try
        {
            foreach (var p in promoted)
                await notifications.CreateAsync(p.UserId, NotificationType.LeaguePromoted,
                    "Você subiu de liga! 🎉", $"Bem-vindo à Liga {p.NewTier}. Continue assim!", "/liga", ct);
            foreach (var r in relegated)
                await notifications.CreateAsync(r.UserId, NotificationType.LeagueRelegated,
                    "Você caiu de liga", $"Foi pra Liga {r.NewTier}. Bora reagir esta semana!", "/liga", ct);
        }
        catch { /* best-effort */ }
    }

    // ── semana (segunda-feira, ISO) ──────────────────────────────────

    private static string WeekKey(DateTime now)
    {
        var d = now.Date;
        var delta = ((int)d.DayOfWeek + 6) % 7; // 0 = segunda
        return d.AddDays(-delta).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static DateTime WeekEnd(string weekKey) =>
        DateTime.ParseExact(weekKey, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(7);
}
