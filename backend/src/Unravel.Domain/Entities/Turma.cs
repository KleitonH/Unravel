namespace Unravel.Domain.Entities;

/// <summary>
/// Status do vínculo de um aluno numa turma. O professor convida (Invited);
/// o aluno aceita (Active). Recusar remove o registro (permite reconvite).
/// </summary>
public enum TurmaMemberStatus
{
    Invited = 0, // convidado, aguardando aceite do aluno
    Active  = 1, // membro ativo da turma
}

/// <summary>
/// Turma criada por um professor (papel Moderator) — agrupa alunos da
/// plataforma. Hoje serve de roster pro modo Kahoot (o professor só pode
/// incluir alunos da própria turma). Visibilidade de trilhas custom por
/// turma fica como evolução futura.
/// </summary>
public class Turma
{
    public int      Id          { get; set; }
    public Guid     OwnerUserId { get; set; } // professor dono (Moderator)
    public string   Name        { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public string?  Emblem      { get; set; } // emoji/ícone curto
    public bool     IsActive    { get; set; } = true;
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

    public User? Owner { get; set; }
    public ICollection<TurmaMember> Members { get; set; } = new List<TurmaMember>();
}

/// <summary>Vínculo aluno↔turma (convite/membro).</summary>
public class TurmaMember
{
    public int               Id        { get; set; }
    public int               TurmaId   { get; set; }
    public Guid              UserId    { get; set; }
    public TurmaMemberStatus Status    { get; set; } = TurmaMemberStatus.Invited;
    public DateTime          InvitedAt { get; set; } = DateTime.UtcNow;
    public DateTime?         JoinedAt  { get; set; }

    public Turma? Turma { get; set; }
    public User?  User  { get; set; }
}
