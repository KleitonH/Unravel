namespace Unravel.Domain.Forge;

/// <summary>
/// PR 50 — histórico de tentativas do aluno no Boss Fight de uma trilha.
/// Singleton por (UserId, TrailId): cada submit atualiza esse registro,
/// preservando o melhor score e a data da primeira vitória.
///
/// <para><b>Por que entidade dedicada e não JournalSnapshot</b>: queries
/// específicas de "ranking dos boss vencidos" e "alunos que ainda não
/// tentaram" são primárias na admin/dashboard futura — tabela própria
/// torna isso O(1).</para>
/// </summary>
public sealed class UserBossFight
{
    public Guid     UserId        { get; set; }
    public int      TrailId       { get; set; }
    public int      AttemptCount  { get; set; }
    public int      BestScore     { get; set; }  // 0..N (count de acertos)
    public int      LastScore     { get; set; }
    public DateTime LastAttemptAt { get; set; }
    public DateTime? FirstWonAt   { get; set; }  // null se nunca passou (≥ 70%)
}
