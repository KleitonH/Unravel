namespace Unravel.Application.Journey.Ports;

/// <summary>
/// Saída pro mundo externo (notificações ao usuário, dashboards live).
/// O cron diário publica um <see cref="DailyPlanGenerated"/> por
/// (userId, trailId) processado; o consumidor decide o que faz com isso.
///
/// <para>Hoje (PR 7): implementação loga. PR 8 (SignalR) liga em hub
/// real e empurra para clientes conectados. Manter a porta aqui permite
/// trocar a implementação sem tocar o cron.</para>
/// </summary>
public interface IJourneyEventBus
{
    Task PublishAsync(JourneyEvent evt, CancellationToken ct = default);
}

/// <summary>Marcador da família de eventos do Journey.</summary>
public abstract record JourneyEvent;

public sealed record DailyPlanGenerated(
    Guid     UserId,
    int      TrailId,
    DateTime PlanDate,
    int      MetaDia,
    int      ExtraPenalty,
    bool?    MetGoalYesterday) : JourneyEvent;

public sealed record StreakReset(
    Guid     UserId,
    int      PreviousStreak,
    DateTime ResetAt) : JourneyEvent;
