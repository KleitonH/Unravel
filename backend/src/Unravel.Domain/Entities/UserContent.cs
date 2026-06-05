namespace Unravel.Domain.Entities;

/// <summary>
/// Estado de progressão de um <see cref="Content"/> pra um usuário.
/// PR 40 estende com gating tipo Super Mario World:
///
/// <para><b>Status</b> orquestra o mapa da trilha:</para>
/// <list type="bullet">
///   <item><c>Locked</c> — contents subsequentes ao último <c>Completed</c>.
///   Não aparece UserContent persistido enquanto bloqueado (ausência
///   da linha = locked).</item>
///   <item><c>Available</c> — primeiro content da trilha sempre, ou o
///   próximo após um Completed. UserContent existe com
///   <c>ChallengesCompleted=0</c>.</item>
///   <item><c>InProgress</c> — aluno respondeu pelo menos 1 desafio
///   (<c>ChallengesCompleted &gt; 0</c>) mas ainda não atingiu meta.</item>
///   <item><c>Completed</c> — <c>ChallengesCompleted &gt;= Content.ChallengesRequired</c>.
///   Marca <c>CompletedAt</c> e dispara unlock do próximo.</item>
/// </list>
///
/// <para>Revisita não desfaz progressão — uma vez <c>Completed</c>,
/// futuros submits continuam atualizando mastery/gamificação mas não
/// "descompletam" a ilha.</para>
/// </summary>
public class UserContent
{
    public int      Id          { get; set; }
    public Guid     UserId      { get; set; }
    public User     User        { get; set; } = null!;
    public int      ContentId   { get; set; }
    public Content  Content     { get; set; } = null!;
    public bool     IsCompleted { get; set; } = false;
    public DateTime StartedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>PR 40 — desafios desse content já respondidos pelo
    /// usuário. Incrementado pelo hook de <c>SubmitPoolChallengeUseCase</c>.
    /// Cap implícito em <c>Content.ChallengesRequired</c> — submits após
    /// Completed continuam incrementando (histórico) mas não voltam
    /// status pra InProgress.</summary>
    public int            ChallengesCompleted { get; set; }

    /// <summary>PR 40 — derivado de ChallengesCompleted mas persistido pra
    /// queries de mapa serem O(1) sem JOIN com Content. Atualizado no
    /// mesmo hook que incrementa contagem.</summary>
    public UserContentStatus Status { get; set; } = UserContentStatus.Available;
}

public enum UserContentStatus
{
    Locked     = 0,   // só conceitual — ausência da linha de UserContent já implica locked
    Available  = 1,   // desbloqueado, ainda não tocou
    InProgress = 2,   // respondeu ≥1 desafio, faltam pra meta
    Completed  = 3,   // atingiu meta — próximo na trilha foi auto-desbloqueado
}
