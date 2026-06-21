using Microsoft.EntityFrameworkCore;
using Unravel.Application.Social.Ports;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Social;

/// <summary>
/// PR 67 — meta coletiva diária. Acumula o XP do dia na caixinha do usuário e,
/// ao bater a meta, dá bônus de moedas a todos os membros (uma vez por dia).
/// </summary>
public class CaixinhaContributionService(ApplicationDbContext db) : ICaixinhaContributionService
{
    /// <summary>Bônus de moedas por membro quando a caixinha bate a meta diária.</summary>
    public const int DailyBonusCoins = 20;

    public async Task ContributeAsync(Guid userId, int xpEarned, DateTime now, CancellationToken ct = default)
    {
        if (xpEarned <= 0) return;

        var member = await db.CaixinhaMember.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);
        if (member is null) return;

        var caixinha = await db.Caixinha.FirstOrDefaultAsync(c => c.Id == member.CaixinhaId, ct);
        if (caixinha is null) return;

        var today = now.Date;

        // Vira o dia → zera o acumulado e o flag de bônus.
        if (caixinha.DailyPointsDate?.Date != today)
        {
            caixinha.DailyPoints = 0;
            caixinha.DailyPointsDate = today;
            caixinha.DailyGoalAwardedAt = null;
        }

        caixinha.DailyPoints += xpEarned;

        // Bateu a meta e ainda não premiou hoje → bônus pra todos.
        if (caixinha.DailyPoints >= caixinha.DailyGoal && caixinha.DailyGoalAwardedAt?.Date != today)
        {
            caixinha.DailyGoalAwardedAt = now;

            var memberIds = await db.CaixinhaMember
                .Where(m => m.CaixinhaId == caixinha.Id)
                .Select(m => m.UserId)
                .ToListAsync(ct);

            var users = await db.User.Where(u => memberIds.Contains(u.Id)).ToListAsync(ct);
            foreach (var u in users)
                u.Coins += DailyBonusCoins;
        }

        await db.SaveChangesAsync(ct);
    }
}
