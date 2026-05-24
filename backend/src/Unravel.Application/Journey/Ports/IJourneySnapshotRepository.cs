using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.Ports;

public interface IJourneySnapshotRepository
{
    /// <summary>Snapshot do user×trilha numa data específica, ou null.
    /// Usado pelo cron para localizar o snapshot do dia anterior e
    /// avaliar cumprimento de meta.</summary>
    Task<JourneySnapshot?> GetByUserTrailDateAsync(
        Guid userId, int trailId, DateTime planDate, CancellationToken ct = default);

    /// <summary>Upsert pela unique key (UserId, TrailId, PlanDate).
    /// Idempotente — re-execução do cron no mesmo dia atualiza, não duplica.</summary>
    Task UpsertAsync(JourneySnapshot snapshot, CancellationToken ct = default);

    /// <summary>Atualiza <c>MetGoal</c> sem reescrever o blob inteiro. O cron
    /// do dia seguinte usa esta operação para fechar o snapshot de ontem
    /// antes de gerar o de hoje.</summary>
    Task MarkGoalAsync(
        Guid userId, int trailId, DateTime planDate, bool met, CancellationToken ct = default);
}
