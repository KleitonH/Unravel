using Microsoft.EntityFrameworkCore;
using Unravel.Application.Classes.Ports;
using Unravel.Application.Notifications.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Classes;

/// <summary>
/// Turmas — vínculo professor↔aluno. Acessa o DbContext direto (mesmo padrão
/// de FriendshipService/CaixinhaService). Professor = dono (Moderator);
/// alunos são convidados e aceitam. Convite gera notificação (best-effort).
/// </summary>
public class TurmaService(ApplicationDbContext db, INotificationService notifications) : ITurmaService
{
    // ── Professor (dono) ──────────────────────────────────────────────

    public async Task<TurmaDto> CreateAsync(Guid ownerId, string name, string? description, string? emblem, CancellationToken ct = default)
    {
        var turma = new Turma
        {
            OwnerUserId = ownerId,
            Name        = (name ?? string.Empty).Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Emblem      = string.IsNullOrWhiteSpace(emblem) ? null : emblem.Trim(),
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow,
        };
        db.Turma.Add(turma);
        await db.SaveChangesAsync(ct);

        var ownerName = await OwnerNameAsync(ownerId, ct);
        return new TurmaDto(turma.Id, turma.Name, turma.Description, turma.Emblem,
            ownerId, ownerName, 0, 0, turma.CreatedAt.ToString("dd/MM/yyyy"));
    }

    public async Task<IReadOnlyList<TurmaDto>> GetOwnedAsync(Guid ownerId, CancellationToken ct = default)
    {
        var ownerName = await OwnerNameAsync(ownerId, ct);
        var rows = await db.Turma
            .AsNoTracking()
            .Where(t => t.OwnerUserId == ownerId && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id, t.Name, t.Description, t.Emblem, t.CreatedAt,
                Active  = t.Members.Count(m => m.Status == TurmaMemberStatus.Active),
                Pending = t.Members.Count(m => m.Status == TurmaMemberStatus.Invited),
            })
            .ToListAsync(ct);

        return rows.Select(t => new TurmaDto(
            t.Id, t.Name, t.Description, t.Emblem, ownerId, ownerName,
            t.Active, t.Pending, t.CreatedAt.ToString("dd/MM/yyyy"))).ToList();
    }

    public async Task<TurmaDetailDto?> GetDetailAsync(Guid ownerId, int turmaId, CancellationToken ct = default)
    {
        var turma = await db.Turma.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == turmaId && t.OwnerUserId == ownerId && t.IsActive, ct);
        if (turma is null) return null;

        var members = await db.TurmaMember
            .AsNoTracking()
            .Where(m => m.TurmaId == turmaId)
            .Select(m => new
            {
                MemberId = m.Id,
                m.Status,
                m.UserId,
                Name        = m.User!.Name,
                m.User.Xp,
                m.User.ActiveTitle,
            })
            .ToListAsync(ct);

        var mapped = members
            .OrderBy(m => m.Status == TurmaMemberStatus.Active ? 0 : 1)
            .ThenByDescending(m => m.Xp)
            .Select(m => new TurmaMemberDto(
                m.MemberId, m.UserId, m.Name, m.Xp, m.ActiveTitle,
                m.Status == TurmaMemberStatus.Active ? "active" : "invited"))
            .ToList();

        return new TurmaDetailDto(turma.Id, turma.Name, turma.Description, turma.Emblem, mapped);
    }

    public async Task<IReadOnlyList<TurmaStudentSearchDto>> SearchStudentsAsync(Guid ownerId, int turmaId, string query, int take, CancellationToken ct = default)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length < 2) return [];

        var owns = await db.Turma.AsNoTracking()
            .AnyAsync(t => t.Id == turmaId && t.OwnerUserId == ownerId && t.IsActive, ct);
        if (!owns) return [];

        var lower = query.ToLower();

        // Vínculos já existentes nessa turma (pra anotar relação).
        var existing = await db.TurmaMember
            .AsNoTracking()
            .Where(m => m.TurmaId == turmaId)
            .Select(m => new { m.UserId, m.Status })
            .ToListAsync(ct);

        var matches = await db.User
            .AsNoTracking()
            .Where(u => u.IsActive
                     && u.Role == Role.Student
                     && u.Name.ToLower().Contains(lower))
            .OrderByDescending(u => u.Xp)
            .Take(take <= 0 ? 20 : Math.Min(take, 50))
            .Select(u => new { u.Id, u.Name, u.Xp, u.ActiveTitle })
            .ToListAsync(ct);

        string Relation(Guid id)
        {
            var link = existing.FirstOrDefault(e => e.UserId == id);
            if (link is null) return "none";
            return link.Status == TurmaMemberStatus.Active ? "member" : "invited";
        }

        return matches
            .Select(u => new TurmaStudentSearchDto(u.Id, u.Name, u.Xp, u.ActiveTitle, Relation(u.Id)))
            .ToList();
    }

    public async Task<TurmaActionResult> InviteAsync(Guid ownerId, int turmaId, Guid studentId, CancellationToken ct = default)
    {
        var turma = await db.Turma.FirstOrDefaultAsync(t => t.Id == turmaId && t.IsActive, ct);
        if (turma is null) return new TurmaActionResult(TurmaActionOutcome.NotFound);
        if (turma.OwnerUserId != ownerId) return new TurmaActionResult(TurmaActionOutcome.NotAuthorized);

        var student = await db.User.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentId && u.IsActive, ct);
        if (student is null) return new TurmaActionResult(TurmaActionOutcome.NotFound);
        if (student.Role != Role.Student) return new TurmaActionResult(TurmaActionOutcome.NotAStudent);

        var existing = await db.TurmaMember.FirstOrDefaultAsync(m => m.TurmaId == turmaId && m.UserId == studentId, ct);
        if (existing is not null)
            return new TurmaActionResult(
                existing.Status == TurmaMemberStatus.Active ? TurmaActionOutcome.AlreadyMember : TurmaActionOutcome.AlreadyInvited,
                existing.Id);

        var member = new TurmaMember
        {
            TurmaId   = turmaId,
            UserId    = studentId,
            Status    = TurmaMemberStatus.Invited,
            InvitedAt = DateTime.UtcNow,
        };
        db.TurmaMember.Add(member);
        await db.SaveChangesAsync(ct);

        var ownerName = await OwnerNameAsync(ownerId, ct);
        await NotifySafe(studentId, NotificationType.ClassInvite, "Convite de turma",
            $"{ownerName} convidou você pra turma \"{turma.Name}\".", "/profile", ct);

        return new TurmaActionResult(TurmaActionOutcome.Ok, member.Id);
    }

    public async Task<TurmaActionResult> RemoveMemberAsync(Guid ownerId, int turmaId, Guid studentId, CancellationToken ct = default)
    {
        var turma = await db.Turma.AsNoTracking().FirstOrDefaultAsync(t => t.Id == turmaId, ct);
        if (turma is null) return new TurmaActionResult(TurmaActionOutcome.NotFound);
        if (turma.OwnerUserId != ownerId) return new TurmaActionResult(TurmaActionOutcome.NotAuthorized);

        var member = await db.TurmaMember.FirstOrDefaultAsync(m => m.TurmaId == turmaId && m.UserId == studentId, ct);
        if (member is null) return new TurmaActionResult(TurmaActionOutcome.NotFound);

        db.TurmaMember.Remove(member);
        await db.SaveChangesAsync(ct);
        return new TurmaActionResult(TurmaActionOutcome.Ok);
    }

    public async Task<TurmaActionResult> ArchiveAsync(Guid ownerId, int turmaId, CancellationToken ct = default)
    {
        var turma = await db.Turma.FirstOrDefaultAsync(t => t.Id == turmaId, ct);
        if (turma is null) return new TurmaActionResult(TurmaActionOutcome.NotFound);
        if (turma.OwnerUserId != ownerId) return new TurmaActionResult(TurmaActionOutcome.NotAuthorized);

        turma.IsActive = false;
        await db.SaveChangesAsync(ct);
        return new TurmaActionResult(TurmaActionOutcome.Ok, turma.Id);
    }

    // ── Aluno ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TurmaDto>> GetMineAsync(Guid studentId, CancellationToken ct = default)
    {
        var rows = await db.TurmaMember
            .AsNoTracking()
            .Where(m => m.UserId == studentId
                     && m.Status == TurmaMemberStatus.Active
                     && m.Turma!.IsActive)
            .Select(m => new
            {
                m.Turma!.Id, m.Turma.Name, m.Turma.Description, m.Turma.Emblem, m.Turma.CreatedAt,
                m.Turma.OwnerUserId, OwnerName = m.Turma.Owner!.Name,
                Active = m.Turma.Members.Count(x => x.Status == TurmaMemberStatus.Active),
            })
            .ToListAsync(ct);

        return rows.Select(t => new TurmaDto(
            t.Id, t.Name, t.Description, t.Emblem, t.OwnerUserId, t.OwnerName,
            t.Active, 0, t.CreatedAt.ToString("dd/MM/yyyy"))).ToList();
    }

    public async Task<IReadOnlyList<TurmaInviteDto>> GetInvitesAsync(Guid studentId, CancellationToken ct = default)
    {
        var rows = await db.TurmaMember
            .AsNoTracking()
            .Where(m => m.UserId == studentId
                     && m.Status == TurmaMemberStatus.Invited
                     && m.Turma!.IsActive)
            .OrderByDescending(m => m.InvitedAt)
            .Select(m => new
            {
                m.Id, m.Turma!.Name, m.Turma.Emblem, OwnerName = m.Turma.Owner!.Name, m.TurmaId, m.InvitedAt,
            })
            .ToListAsync(ct);

        return rows.Select(m => new TurmaInviteDto(
            m.Id, m.TurmaId, m.Name, m.Emblem, m.OwnerName,
            m.InvitedAt.ToString("dd/MM/yyyy"))).ToList();
    }

    public async Task<TurmaActionResult> RespondInviteAsync(Guid studentId, int memberId, bool accept, CancellationToken ct = default)
    {
        var member = await db.TurmaMember.Include(m => m.Turma).FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return new TurmaActionResult(TurmaActionOutcome.NotFound);

        // Só o próprio convidado responde, e só enquanto convite pendente.
        if (member.UserId != studentId || member.Status != TurmaMemberStatus.Invited)
            return new TurmaActionResult(TurmaActionOutcome.NotAuthorized);

        if (accept)
        {
            member.Status   = TurmaMemberStatus.Active;
            member.JoinedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            if (member.Turma is not null)
            {
                var studentName = await db.User.AsNoTracking().Where(u => u.Id == studentId).Select(u => u.Name).FirstOrDefaultAsync(ct);
                await NotifySafe(member.Turma.OwnerUserId, NotificationType.ClassInvite, "Convite aceito",
                    $"{studentName} entrou na turma \"{member.Turma.Name}\".", "/admin/trails", ct);
            }
        }
        else
        {
            // Recusar remove o registro → permite reconvite depois.
            db.TurmaMember.Remove(member);
            await db.SaveChangesAsync(ct);
        }

        return new TurmaActionResult(TurmaActionOutcome.Ok, member.Id);
    }

    public async Task<TurmaActionResult> LeaveAsync(Guid studentId, int turmaId, CancellationToken ct = default)
    {
        var member = await db.TurmaMember.FirstOrDefaultAsync(m => m.TurmaId == turmaId && m.UserId == studentId, ct);
        if (member is null) return new TurmaActionResult(TurmaActionOutcome.NotFound);

        db.TurmaMember.Remove(member);
        await db.SaveChangesAsync(ct);
        return new TurmaActionResult(TurmaActionOutcome.Ok);
    }

    // ── helpers ───────────────────────────────────────────────────────

    private Task<string> OwnerNameAsync(Guid ownerId, CancellationToken ct)
        => db.User.AsNoTracking().Where(u => u.Id == ownerId).Select(u => u.Name).FirstOrDefaultAsync(ct)!;

    private async Task NotifySafe(Guid userId, NotificationType type, string title, string body, string? link, CancellationToken ct)
    {
        try { await notifications.CreateAsync(userId, type, title, body, link, ct); }
        catch { /* best-effort — notificação não pode derrubar a ação */ }
    }
}
