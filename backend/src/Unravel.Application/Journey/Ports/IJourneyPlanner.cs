using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.Ports;

/// <summary>
/// Decide o que o usuário deve estudar hoje (e nos próximos dias). Função
/// pura: dado o grafo de conhecimento, as masteries atuais e o estado do
/// usuário (vidas/streak), produz um <see cref="JourneyPlan"/> determinístico.
/// Sem efeitos colaterais — não persiste, não lê BD.
///
/// <para>É port da Application porque o algoritmo é regra de negócio
/// central, não detalhe de infraestrutura. A implementação fica em
/// Application também (não em Infrastructure) pelo mesmo motivo.</para>
/// </summary>
public interface IJourneyPlanner
{
    JourneyPlan Plan(JourneyPlanInput input);
}

/// <summary>Tudo que o planner precisa para decidir, agrupado para evitar
/// crescimento da assinatura. Imutável.</summary>
public sealed record JourneyPlanInput(
    Guid                  UserId,
    KnowledgeGraph        Graph,
    IReadOnlyList<Mastery> Masteries,
    int                   LivesAvailable,
    int                   StreakDays,
    DateTime              AsOf,
    JourneyPlannerOptions? Options = null
);

/// <summary>Pesos do ranking e bounds do <c>metaDia</c>, em um lugar só
/// para facilitar tuning sem mexer no algoritmo.</summary>
public sealed record JourneyPlannerOptions
{
    public double WeightNeed         { get; init; } = 1.00;
    public double WeightUnlock       { get; init; } = 0.40;
    public double WeightSrsOverdue   { get; init; } = 0.80;
    public double WeightDifficultyFit{ get; init; } = 0.30;

    /// <summary>Quantidade mínima e máxima de itens no <c>Today</c>.
    /// Limita o efeito de vidas zero (mínimo 1 sempre que houver
    /// candidato) e de streaks gigantes (cap em 8 — virou padrão "muito").</summary>
    public int MinMetaDia { get; init; } = 1;
    public int MaxMetaDia { get; init; } = 8;

    /// <summary>Quantos dias projetar à frente em <c>Upcoming</c>. Não é
    /// agenda fechada — só prévia. 5 cobre uma "semana útil" de estudo.</summary>
    public int UpcomingDays { get; init; } = 5;

    /// <summary>Mastery a partir do qual consideramos o tópico "dominado"
    /// para fins de gating de pré-requisitos.</summary>
    public double MasteryThreshold { get; init; } = 0.70;

    public static readonly JourneyPlannerOptions Default = new();
}
