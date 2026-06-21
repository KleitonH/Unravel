using Microsoft.EntityFrameworkCore;
using Unravel.Application.Notifications.Ports;
using Unravel.Application.Social.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Social;

/// <summary>
/// PR 67 — meta coletiva diária. Acumula o XP do dia na caixinha do usuário e,
/// ao bater a meta, dá bônus de moedas a todos os membros (uma vez por dia).
/// </summary>
public class CaixinhaContributionService(ApplicationDbContext db, INotificationService notifications) : ICaixinhaContributionService
{
    /// <summary>Bônus de moedas por membro quando a caixinha bate a meta diária.</summary>
    public const int DailyBonusCoins = 20;

    /// <summary>Bônus por dia de ofensiva coletiva, por membro (capado em 10 dias).</summary>
    public const int StreakBonusPerDay = 5;

    public async Task ContributeAsync(Guid userId, int xpEarned, DateTime now, CancellationToken ct = default)
    {
        if (xpEarned <= 0) return;

        var member = await db.CaixinhaMember.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);
        if (member is null) return;

        var caixinha = await db.Caixinha.FirstOrDefaultAsync(c => c.Id == member.CaixinhaId, ct);
        if (caixinha is null) return;

        var today = now.Date;

        // Membros da caixinha (rastreados — pode creditar bônus de moedas).
        var memberIds = await db.CaixinhaMember
            .Where(m => m.CaixinhaId == caixinha.Id)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        var members = await db.User.Where(u => memberIds.Contains(u.Id)).ToListAsync(ct);

        // ── Meta coletiva diária ──
        if (caixinha.DailyPointsDate?.Date != today)
        {
            caixinha.DailyPoints = 0;
            caixinha.DailyPointsDate = today;
            caixinha.DailyGoalAwardedAt = null;
        }
        caixinha.DailyPoints += xpEarned;

        var goalJustReached = false;
        if (caixinha.DailyPoints >= caixinha.DailyGoal && caixinha.DailyGoalAwardedAt?.Date != today)
        {
            caixinha.DailyGoalAwardedAt = now;
            foreach (var u in members) u.Coins += DailyBonusCoins;
            goalJustReached = true;
        }

        // ── Ofensiva coletiva ── (todos ativos hoje → avança 1x/dia)
        var allActive = members.Count > 0 && members.All(u => u.LastActivityDate?.Date == today);
        if (allActive && caixinha.StreakLastDate?.Date != today)
        {
            var yesterday = today.AddDays(-1);
            caixinha.StreakDays = caixinha.StreakLastDate?.Date == yesterday ? caixinha.StreakDays + 1 : 1;
            caixinha.StreakLastDate = today;

            var streakBonus = Math.Min(caixinha.StreakDays, 10) * StreakBonusPerDay;
            foreach (var u in members) u.Coins += streakBonus;
        }

        await db.SaveChangesAsync(ct);

        if (goalJustReached)
        {
            try
            {
                await notifications.CreateManyAsync(memberIds, NotificationType.CaixinhaGoal,
                    "Meta diária batida! 🎉",
                    $"A {caixinha.Name} bateu a meta de hoje — +{DailyBonusCoins} 🪙 pra todo mundo.",
                    "/caixinha", ct);
            }
            catch { /* best-effort */ }
        }
    }
}
