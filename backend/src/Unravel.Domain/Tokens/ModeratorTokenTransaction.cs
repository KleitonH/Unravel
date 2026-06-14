namespace Unravel.Domain.Tokens;

/// <summary>
/// PR 52 — histórico imutável de movimentações no saldo de lã.
/// Cada Credit ou Debit gera uma linha. Auditoria total — saldo
/// final deve sempre bater com SUM(deltas) por usuário.
///
/// <para><b>Metadata</b> é JSON livre pra capturar contexto: id da pergunta
/// gerada, id do bulk job, id do aluno que votou, etc. Permite reconstruir
/// "por que ganhou/gastou" sem novas colunas.</para>
/// </summary>
public sealed class ModeratorTokenTransaction
{
    public long                 Id          { get; set; }
    public Guid                 UserId      { get; set; }
    public int                  DeltaCm     { get; set; }   // + ganho, − gasto
    public TokenTransactionReason Reason    { get; set; }
    public string?              Metadata    { get; set; }   // JSON livre
    public DateTime             CreatedAt   { get; set; }
}

/// <summary>Razões possíveis pra um delta. Valores numéricos fixos
/// pra estabilidade no DB. Novos motivos viram entries adicionais —
/// nunca renumere.</summary>
public enum TokenTransactionReason
{
    // ── Créditos (positivos) ────────────────────────────────────
    WelcomeBonus        = 1,   // +1000 ao virar moderador
    MonthlyRefill       = 2,   // +500 mensal
    StudentCompletedIsland = 10,   // +1 por aluno completando ilha em conteúdo seu
    StudentVotedGood    = 11,   // +2 por voto 👍
    BossFightWon        = 12,   // +5 por aluno vencendo boss da sua trilha
    QuestionPromotedToGold = 13,   // +20 quando pergunta vira gold set
    TrailMilestone10    = 20,   // +50 trilha alcança 10 alunos
    TrailMilestone50    = 21,   // +200 alcança 50
    TrailMilestone100   = 22,   // +500 alcança 100
    ForgeRefund         = 30,   // PR 62 — estorno de job que falhou de vez sem reposição possível

    // ── Débitos (negativos) ─────────────────────────────────────
    ForgeNormal         = 100,  // −1 por job normal
    ForgeUrgent         = 101,  // −3 por job urgent
    PromoteToGold       = 110,  // −5 promover pergunta gerada a gold

    // ── Administrativo ─────────────────────────────────────────
    AdminGrant          = 200,  // ajuste manual via admin
    AdminRevoke         = 201,  // débito manual via admin
}
