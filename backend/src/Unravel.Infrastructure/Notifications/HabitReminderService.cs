using Microsoft.EntityFrameworkCore;
using Unravel.Application.Notifications.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Notifications;

/// <summary>
/// PR 70 — lembrete de hábito. Notifica alunos com ofensiva ativa que ainda
/// não estudaram hoje ("ofensiva em risco"). Idempotente por dia: não repete o
/// lembrete pro mesmo usuário no mesmo dia.
/// </summary>
public class HabitReminderService(ApplicationDbContext db) : IHabitReminderService
{
    public async Task<int> RunAsync(DateTime now, CancellationToken ct = default)
    {
        var today = now.Date;

        // Alunos ativos com ofensiva >= 1 que NÃO estudaram hoje.
        var atRisk = await db.User.AsNoTracking()
            .Where(u => u.IsActive
                     && u.Role == Role.Student
                     && u.StreakDays >= 1
                     && (u.LastActivityDate == null || u.LastActivityDate.Value.Date < today))
            .Select(u => new { u.Id, u.StreakDays })
            .ToListAsync(ct);
        if (atRisk.Count == 0) return 0;

        var ids = atRisk.Select(a => a.Id).ToList();

        // Dedup: quem já recebeu lembrete de ofensiva hoje não recebe de novo.
        var alreadyToday = (await db.Notification.AsNoTracking()
            .Where(n => ids.Contains(n.UserId)
                     && n.Type == NotificationType.StreakRisk
                     && n.CreatedAt >= today)
            .Select(n => n.UserId)
            .ToListAsync(ct)).ToHashSet();

        var fresh = atRisk.Where(a => !alreadyToday.Contains(a.Id)).ToList();
        if (fresh.Count == 0) return 0;

        foreach (var a in fresh)
        {
            db.Notification.Add(new Notification
            {
                UserId    = a.Id,
                Type      = NotificationType.StreakRisk,
                Title     = "Sua ofensiva está em risco! 🔥",
                Body      = $"Você tem {a.StreakDays} dia(s) de ofensiva. Estude hoje pra não perder!",
                Link      = "/dashboard",
                CreatedAt = now,
            });
        }
        await db.SaveChangesAsync(ct);
        return fresh.Count;
    }
}
