namespace Unravel.Application.Journey.Ports;

/// <summary>
/// Leituras agregadas que o <c>DailyReplanService</c> precisa do banco.
/// Mantém Application livre de DbContext.
/// </summary>
public interface IDailyReplanReadModel
{
    /// <summary>Pares (UserId, TrailId) candidatos ao replanejamento de
    /// hoje. Filtros aplicados:
    /// <list type="bullet">
    ///   <item><c>User.IsActive = true</c></item>
    ///   <item><c>UserTrail.IsActive = true</c></item>
    ///   <item><c>Trail.IsActive = true</c></item>
    /// </list>
    /// </summary>
    Task<IReadOnlyList<ReplanTarget>> GetActiveTargetsAsync(CancellationToken ct = default);

    /// <summary>Quantos challenges o user submeteu numa janela. Usado para
    /// avaliar se cumpriu meta de ontem.</summary>
    Task<int> CountUserChallengesSubmittedAsync(
        Guid userId, DateTime fromInclusiveUtc, DateTime toExclusiveUtc, CancellationToken ct = default);

    /// <summary>Tudo que o cron precisa do User para tomar decisões de
    /// gamificação (streak/vidas/última atividade).</summary>
    Task<UserCronSnapshot?> GetUserCronSnapshotAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Persiste mudanças no User (streak reset, p.ex.).</summary>
    Task UpdateUserStreakAsync(Guid userId, int newStreak, CancellationToken ct = default);
}

public sealed record ReplanTarget(Guid UserId, int TrailId);

public sealed record UserCronSnapshot(
    int       Lives,
    int       StreakDays,
    DateTime? LastActivityDate);
