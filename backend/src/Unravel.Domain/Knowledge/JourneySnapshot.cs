namespace Unravel.Domain.Knowledge;

/// <summary>
/// "Foto" persistida de um <see cref="JourneyPlan"/> gerado pelo cron
/// diário (PR 7). Uma linha por (UserId, TrailId, Date UTC). Idempotência
/// garantida via unique index em <c>(user_id, trail_id, plan_date)</c>:
/// se o cron rodar duas vezes no mesmo dia, a segunda execução faz upsert
/// e não duplica.
///
/// <para><b>Por que persistir o plano</b>: até aqui, <c>JourneyPlan</c> era
/// efêmero (calculado a cada GET /api/journey/today). Com o cron, precisamos
/// (a) rastrear se o user cumpriu a meta do dia anterior — exige comparar
/// com o que foi prometido, (b) aplicar penalidade (+1 challenge) no plano
/// novo se não cumpriu, e (c) servir o plano do dia mesmo se o user não
/// abriu o app desde a virada (não recalcula no GET, lê o snapshot).</para>
/// </summary>
public sealed class JourneySnapshot
{
    public int      Id           { get; set; }
    public Guid     UserId       { get; set; }
    public int      TrailId      { get; set; }
    /// <summary>Data UTC (00:00:00) à qual o snapshot se refere.</summary>
    public DateTime PlanDate     { get; set; }
    public int      MetaDia      { get; set; }
    public int      ExtraChallengesPenalty { get; set; }
    /// <summary>JSON do <see cref="JourneyPlan"/> serializado pro frontend
    /// consumir. Mantemos como blob porque a estrutura nunca é consultada
    /// parcialmente — só lida inteira na renderização do dia.</summary>
    public string   PlanJson     { get; set; } = string.Empty;
    public DateTime GeneratedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>true se o usuário cumpriu a <c>MetaDia</c> deste snapshot.
    /// Avaliado pelo cron do dia seguinte (n+1) e persistido aqui — assim
    /// o relatório de "% de metas cumpridas" é uma agregação trivial sem
    /// JOIN com tabela de submissões.</summary>
    public bool?    MetGoal      { get; set; }
}
