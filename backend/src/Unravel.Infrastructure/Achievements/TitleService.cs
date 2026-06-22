using Microsoft.EntityFrameworkCore;
using Unravel.Application.Achievements.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Achievements;

/// <summary>
/// Títulos desbloqueáveis + ranking global (Ideia 5). Catálogo semeado de
/// forma idempotente (por Code) na primeira leitura/avaliação — evita
/// plumbing de seeder no startup. Acesso direto ao DbContext.
/// </summary>
public class TitleService(ApplicationDbContext db) : ITitleService
{
    // Catálogo inicial (gato + jargão de TI). Code é a chave de idempotência.
    private static readonly (string Code, string Text, BadgeCategory Cat, TitleCriterion Crit, int Threshold)[] Catalog =
    {
        ("streak-7",   "Gato Persistente",          BadgeCategory.Streak,    TitleCriterion.StreakDays, 7),
        ("streak-30",  "Maine Coon do Hábito",      BadgeCategory.Streak,    TitleCriterion.StreakDays, 30),
        ("streak-100", "Lendário das 100 Noites",   BadgeCategory.Streak,    TitleCriterion.StreakDays, 100),
        ("arena-1",    "Estreante da Arena",        BadgeCategory.Arena,     TitleCriterion.ArenaWins,  1),
        ("arena-10",   "Caçador da Arena",          BadgeCategory.Arena,     TitleCriterion.ArenaWins,  10),
        ("xp-1000",    "CSSiamês Profissional",     BadgeCategory.Knowledge, TitleCriterion.XpTotal,    1000),
        ("xp-5000",    "Mestre dos Bits",           BadgeCategory.Knowledge, TitleCriterion.XpTotal,    5000),
    };

    private async Task EnsureCatalogAsync(CancellationToken ct)
    {
        var existing = await db.Title.Select(t => t.Code).ToListAsync(ct);
        var set = existing.ToHashSet();
        var added = false;
        foreach (var c in Catalog)
            if (!set.Contains(c.Code))
            {
                db.Title.Add(new Title { Code = c.Code, Text = c.Text, Category = c.Cat, Criterion = c.Crit, Threshold = c.Threshold });
                added = true;
            }
        if (added) await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TitleDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        await EnsureCatalogAsync(ct);
        var titles = await db.Title.AsNoTracking().ToListAsync(ct);
        var owned  = (await db.UserTitle.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.TitleId).ToListAsync(ct)).ToHashSet();
        var active = await db.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.ActiveTitle).FirstOrDefaultAsync(ct);

        return titles
            .OrderBy(t => t.Category).ThenBy(t => t.Threshold)
            .Select(t => new TitleDto(t.Id, t.Text, t.Category.ToString(), t.Criterion.ToString(), t.Threshold,
                owned.Contains(t.Id), active == t.Text))
            .ToList();
    }

    public async Task<ActivateTitleOutcome> ActivateAsync(Guid userId, int titleId, CancellationToken ct = default)
    {
        var user = await db.User.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return ActivateTitleOutcome.NotFound;

        if (titleId <= 0) { user.ActiveTitle = null; await db.SaveChangesAsync(ct); return ActivateTitleOutcome.Ok; }

        var title = await db.Title.AsNoTracking().FirstOrDefaultAsync(t => t.Id == titleId, ct);
        if (title is null) return ActivateTitleOutcome.NotFound;

        var owns = await db.UserTitle.AnyAsync(u => u.UserId == userId && u.TitleId == titleId, ct);
        if (!owns) return ActivateTitleOutcome.NotOwned;

        user.ActiveTitle = title.Text;
        await db.SaveChangesAsync(ct);
        return ActivateTitleOutcome.Ok;
    }

    public async Task<IReadOnlyList<string>> EvaluateAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        await EnsureCatalogAsync(ct);

        var user = await db.User.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.StreakDays, u.Xp }).FirstOrDefaultAsync(ct);
        if (user is null) return Array.Empty<string>();

        var arenaWins = await db.ArenaRanking.AsNoTracking().Where(r => r.UserId == userId).Select(r => (int?)r.Wins).FirstOrDefaultAsync(ct) ?? 0;

        var owned = (await db.UserTitle.Where(u => u.UserId == userId).Select(u => u.TitleId).ToListAsync(ct)).ToHashSet();
        var titles = await db.Title.AsNoTracking().ToListAsync(ct);

        var granted = new List<string>();
        foreach (var t in titles)
        {
            if (owned.Contains(t.Id)) continue;
            var meets = t.Criterion switch
            {
                TitleCriterion.StreakDays => user.StreakDays >= t.Threshold,
                TitleCriterion.ArenaWins  => arenaWins      >= t.Threshold,
                TitleCriterion.XpTotal    => user.Xp        >= t.Threshold,
                _ => false,
            };
            if (meets)
            {
                db.UserTitle.Add(new UserTitle { UserId = userId, TitleId = t.Id, EarnedAt = now });
                granted.Add(t.Text);
            }
        }
        if (granted.Count > 0) await db.SaveChangesAsync(ct);
        return granted;
    }

    public async Task<IReadOnlyList<GlobalRankingRow>> GlobalRankingAsync(int top, CancellationToken ct = default)
    {
        var rows = await db.User.AsNoTracking()
            .Where(u => u.IsActive && u.Role == Role.Student)
            .OrderByDescending(u => u.Xp)
            .Take(top <= 0 ? 50 : Math.Min(top, 200))
            .Select(u => new { u.Id, u.Name, u.Xp, u.ActiveTitle })
            .ToListAsync(ct);

        return rows.Select((u, i) => new GlobalRankingRow(i + 1, u.Id, u.Name, u.Xp, u.ActiveTitle)).ToList();
    }
}
