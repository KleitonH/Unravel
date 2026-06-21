namespace Unravel.Application.Classes.Ports;

/// <summary>
/// Turmas — vínculo professor↔aluno. O professor (Moderator) cria turmas e
/// convida alunos da plataforma; o aluno aceita/recusa e vê suas turmas.
/// Roster (membros ativos) alimenta o modo Kahoot.
/// </summary>

/// <summary>Turma na visão de lista (professor: suas turmas; aluno: turmas que participa).</summary>
public record TurmaDto(
    int      Id,
    string   Name,
    string?  Description,
    string?  Emblem,
    Guid     OwnerUserId,
    string   OwnerName,
    int      MemberCount,   // membros ativos
    int      PendingCount,  // convites pendentes
    string   CreatedAt);

/// <summary>Membro/convidado de uma turma (visão do professor). Status: "active" | "invited".</summary>
public record TurmaMemberDto(
    int      MemberId,
    Guid     UserId,
    string   Name,
    int      Xp,
    string?  ActiveTitle,
    string   Status);

public record TurmaDetailDto(
    int      Id,
    string   Name,
    string?  Description,
    string?  Emblem,
    IReadOnlyList<TurmaMemberDto> Members);

/// <summary>Convite pendente na visão do aluno.</summary>
public record TurmaInviteDto(
    int      MemberId,
    int      TurmaId,
    string   TurmaName,
    string?  Emblem,
    string   OwnerName,
    string   InvitedAt);

/// <summary>
/// Resultado de busca de aluno pra convidar. Relation: "none" | "invited" | "member".
/// </summary>
public record TurmaStudentSearchDto(
    Guid     UserId,
    string   Name,
    int      Xp,
    string?  ActiveTitle,
    string   Relation);

public enum TurmaActionOutcome
{
    Ok,
    NotFound,
    NotAuthorized,   // não é o dono da turma / convite não é seu
    AlreadyMember,
    AlreadyInvited,
    NotAStudent,     // só alunos podem ser convidados
}

public record TurmaActionResult(TurmaActionOutcome Outcome, int? Id = null);

public interface ITurmaService
{
    // ── Professor (dono) ──────────────────────────────────────────────
    Task<TurmaDto> CreateAsync(Guid ownerId, string name, string? description, string? emblem, CancellationToken ct = default);
    Task<IReadOnlyList<TurmaDto>> GetOwnedAsync(Guid ownerId, CancellationToken ct = default);
    Task<TurmaDetailDto?> GetDetailAsync(Guid ownerId, int turmaId, CancellationToken ct = default);
    Task<IReadOnlyList<TurmaStudentSearchDto>> SearchStudentsAsync(Guid ownerId, int turmaId, string query, int take, CancellationToken ct = default);
    Task<TurmaActionResult> InviteAsync(Guid ownerId, int turmaId, Guid studentId, CancellationToken ct = default);
    Task<TurmaActionResult> RemoveMemberAsync(Guid ownerId, int turmaId, Guid studentId, CancellationToken ct = default);
    Task<TurmaActionResult> ArchiveAsync(Guid ownerId, int turmaId, CancellationToken ct = default);

    // ── Aluno ─────────────────────────────────────────────────────────
    Task<IReadOnlyList<TurmaDto>> GetMineAsync(Guid studentId, CancellationToken ct = default);
    Task<IReadOnlyList<TurmaInviteDto>> GetInvitesAsync(Guid studentId, CancellationToken ct = default);
    Task<TurmaActionResult> RespondInviteAsync(Guid studentId, int memberId, bool accept, CancellationToken ct = default);
    Task<TurmaActionResult> LeaveAsync(Guid studentId, int turmaId, CancellationToken ct = default);
}
