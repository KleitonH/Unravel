namespace Unravel.Domain.Knowledge;

/// <summary>
/// Snapshot do plano de estudos de um usuário numa trilha para um dia
/// específico. Produzido pelo <c>JourneyPlanner</c>, consumido pelo
/// frontend para montar o dashboard do dia e a prévia dos próximos dias.
/// Imutável.
/// </summary>
public sealed record JourneyPlan(
    Guid                       UserId,
    int                        TrailId,
    DateTime                   GeneratedAt,
    int                        MetaDia,
    IReadOnlyList<JourneyItem> Today,
    IReadOnlyList<JourneyItem> Upcoming
);

/// <summary>Um item da fila — um <see cref="Topic"/> que o usuário deve
/// estudar/revisar, com a justificativa que motivou a seleção e o score
/// usado no ranking (útil pra explainability e debugging).</summary>
public sealed record JourneyItem(
    int          TopicId,
    int          ContentId,
    string       Slug,
    double       Priority,
    JourneyReason Reason,
    double       EffectiveMastery,
    double       DifficultyScore
);

/// <summary>Por que esse item entrou no plano. Permite que a UI mostre
/// chips distintos ("Novo conteúdo", "Hora de revisar", "Reforço") e
/// que métricas de produto separem aquisição de retenção.</summary>
public enum JourneyReason
{
    /// <summary>Tópico nunca visto pelo usuário (Mastery null).</summary>
    NewLearning = 1,
    /// <summary>Tópico já estudado cuja revisão (SRS) venceu.</summary>
    DueReview   = 2,
    /// <summary>Tópico visto recentemente, ainda longe do domínio
    /// (effectiveMastery &lt; 0.7), revisão não vencida — reforço opcional.</summary>
    Reinforce   = 3,
}
