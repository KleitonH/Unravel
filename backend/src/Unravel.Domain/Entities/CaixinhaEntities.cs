namespace Unravel.Domain.Entities;

/// <summary>
/// PR 65 — Caixinha de Gatos (clã/grupo social). Conceito: arquivos_unravel
/// "Ideia 6". Grupo de 3–10 membros; quem cria vira líder; placar coletivo,
/// mural interno e (futuramente) eventos entre caixinhas.
/// </summary>
public enum CaixinhaRole
{
    Member = 0,
    Leader = 1,
}

/// <summary>Grupo social ("caixinha"). Placar coletivo é derivado da soma de
/// XP dos membros (sem write-path próprio nesta fatia).</summary>
public class Caixinha
{
    public int      Id        { get; set; }
    public string   Name      { get; set; } = string.Empty;
    public string   Emblem    { get; set; } = "📦"; // emoji
    public Guid     LeaderId  { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // PR 67 — meta coletiva diária. DailyPoints acumula o XP ganho pelos
    // membros no dia (reseta na virada); ao bater DailyGoal, todos ganham
    // bônus de moedas uma vez no dia (DailyGoalAwardedAt).
    public int       DailyGoal          { get; set; } = 100;
    public int       DailyPoints        { get; set; }
    public DateTime? DailyPointsDate    { get; set; }
    public DateTime? DailyGoalAwardedAt { get; set; }

    // PR 68 — ofensiva de caixinha: dias consecutivos em que TODOS os membros
    // estudaram. Avança 1x/dia quando todos ficam ativos; reseta se um dia passa
    // sem todos. StreakLastDate = último dia em que avançou.
    public int       StreakDays     { get; set; }
    public DateTime? StreakLastDate { get; set; }

    public ICollection<CaixinhaMember>  Members  { get; set; } = new List<CaixinhaMember>();
    public ICollection<CaixinhaMessage> Messages { get; set; } = new List<CaixinhaMessage>();

    /// <summary>Capacidade máxima (conceito Ideia 6: 3–10 membros).</summary>
    public const int MaxMembers = 10;
}

/// <summary>Vínculo usuário↔caixinha. UserId é único globalmente: um usuário
/// pertence a no máximo uma caixinha.</summary>
public class CaixinhaMember
{
    public int          Id         { get; set; }
    public int          CaixinhaId { get; set; }
    public Guid         UserId     { get; set; }
    public CaixinhaRole Role       { get; set; } = CaixinhaRole.Member;
    public DateTime     JoinedAt   { get; set; } = DateTime.UtcNow;

    public Caixinha? Caixinha { get; set; }
    public User?     User     { get; set; }
}

/// <summary>Mensagem do mural interno da caixinha.</summary>
public class CaixinhaMessage
{
    public int      Id         { get; set; }
    public int      CaixinhaId { get; set; }
    public Guid     UserId     { get; set; }
    public string   Text       { get; set; } = string.Empty;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    public Caixinha? Caixinha { get; set; }
    public User?     User     { get; set; }
}
