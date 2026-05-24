using System.Text.Json;
using Microsoft.Extensions.Logging;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey;

/// <summary>
/// O coração do PR 7: orquestra o replanejamento diário. Pode ser chamado:
/// <list type="bullet">
///   <item>Pelo <c>BackgroundService</c> à meia-noite UTC (rota normal).</item>
///   <item>Por um endpoint manual de admin (PR 7 commit C — útil para demo
///   sem esperar a virada).</item>
/// </list>
///
/// <para><b>Por trilha de cada usuário ativo</b>, o serviço:</para>
/// <list type="number">
///   <item>Lê o snapshot de ontem e calcula <c>MetGoal</c>: cumpriu se
///   <c>challengesSubmittedYesterday ≥ snapshot.MetaDia</c>.</item>
///   <item>Persiste <c>MetGoal</c> no snapshot de ontem (encerramento).</item>
///   <item>Decai streak se passou 2+ dias sem atividade (regra do doc
///   Ofensiva).</item>
///   <item>Roda o <c>IJourneyPlanner</c> para hoje.</item>
///   <item>Aplica penalidade: se não cumpriu ontem, adiciona +1 ao
///   <c>MetaDia</c> de hoje (não além de <c>MaxMetaDia</c>).</item>
///   <item>Persiste snapshot de hoje (upsert idempotente).</item>
///   <item>Publica <see cref="DailyPlanGenerated"/> no event bus.</item>
/// </list>
///
/// <para>Falhas por (user, trilha) são logadas mas não param o lote — uma
/// trilha quebrada não pode bloquear o cron inteiro.</para>
/// </summary>
public sealed class DailyReplanService
{
    private readonly IDailyReplanReadModel       _readModel;
    private readonly IJourneySnapshotRepository  _snapshots;
    private readonly IKnowledgeGraphCache        _graphCache;
    private readonly IMasteryRepository          _masteryRepo;
    private readonly IJourneyPlanner             _planner;
    private readonly IJourneyEventBus            _eventBus;
    private readonly ILogger<DailyReplanService> _log;

    /// <summary>Após N dias sem atividade, streak vai a zero. 2 segue a
    /// regra implícita do código existente em <c>ChallengeService.UpdateStreakAsync</c>
    /// (que reseta se <c>lastDate &lt; today.AddDays(-1)</c>).</summary>
    public const int StreakResetAfterDays = 2;

    public DailyReplanService(
        IDailyReplanReadModel      readModel,
        IJourneySnapshotRepository snapshots,
        IKnowledgeGraphCache       graphCache,
        IMasteryRepository         masteryRepo,
        IJourneyPlanner            planner,
        IJourneyEventBus           eventBus,
        ILogger<DailyReplanService> log)
    {
        _readModel   = readModel;
        _snapshots   = snapshots;
        _graphCache  = graphCache;
        _masteryRepo = masteryRepo;
        _planner     = planner;
        _eventBus    = eventBus;
        _log         = log;
    }

    /// <summary>Executa o lote completo. <paramref name="asOf"/> deve ser o
    /// instante "agora" (UTC) — receber por parâmetro permite testar com
    /// tempo arbitrário.</summary>
    public async Task<DailyReplanReport> RunAsync(DateTime asOf, CancellationToken ct = default)
    {
        var today      = asOf.Date;       // 00:00 UTC do dia atual
        var yesterday  = today.AddDays(-1);
        var processed  = 0;
        var failures   = 0;
        var goalMet    = 0;

        var targets = await _readModel.GetActiveTargetsAsync(ct);
        _log.LogInformation("Daily replan starting for {Count} (user, trail) targets at {AsOf:o}",
            targets.Count, asOf);

        // Agrupa por usuário para fazer 1 leitura de UserCronSnapshot por user.
        foreach (var userGroup in targets.GroupBy(t => t.UserId))
        {
            ct.ThrowIfCancellationRequested();

            var user = await _readModel.GetUserCronSnapshotAsync(userGroup.Key, ct);
            if (user is null) continue;

            // Decaimento de streak (1x por user, independente de trilhas).
            var daysSinceActivity = user.LastActivityDate is null
                ? int.MaxValue
                : (int)Math.Floor((today - user.LastActivityDate.Value.Date).TotalDays);

            var effectiveStreak = user.StreakDays;
            if (daysSinceActivity >= StreakResetAfterDays && user.StreakDays > 0)
            {
                effectiveStreak = 0;
                await _readModel.UpdateUserStreakAsync(userGroup.Key, 0, ct);
                await _eventBus.PublishAsync(new StreakReset(userGroup.Key, user.StreakDays, asOf), ct);
                _log.LogInformation("Streak reset for user {UserId} ({Days} days inactive)",
                    userGroup.Key, daysSinceActivity);
            }

            foreach (var target in userGroup)
            {
                try
                {
                    var met = await ProcessOneAsync(target.UserId, target.TrailId,
                                                     today, yesterday, user.Lives, effectiveStreak, ct);
                    processed++;
                    if (met == true) goalMet++;
                }
                catch (Exception ex)
                {
                    failures++;
                    _log.LogWarning(ex,
                        "Falha replanejando user {UserId} trilha {TrailId}; segue lote.",
                        target.UserId, target.TrailId);
                }
            }
        }

        var report = new DailyReplanReport(asOf, processed, failures, goalMet);
        _log.LogInformation("Daily replan done: {Processed} processed, {GoalMet} met goal, {Failures} failures",
            report.Processed, report.YesterdayGoalMet, report.Failures);
        return report;
    }

    private async Task<bool?> ProcessOneAsync(
        Guid userId, int trailId, DateTime today, DateTime yesterday,
        int lives, int streakDays, CancellationToken ct)
    {
        // 1) Avalia snapshot de ontem.
        var yesterdaySnap = await _snapshots.GetByUserTrailDateAsync(userId, trailId, yesterday, ct);

        bool? metYesterday = null;
        if (yesterdaySnap is not null && yesterdaySnap.MetGoal is null)
        {
            var submitted = await _readModel.CountUserChallengesSubmittedAsync(
                userId, yesterday, today, ct);
            metYesterday = submitted >= yesterdaySnap.MetaDia;
            await _snapshots.MarkGoalAsync(userId, trailId, yesterday, metYesterday.Value, ct);
        }
        else if (yesterdaySnap is not null)
        {
            metYesterday = yesterdaySnap.MetGoal;
        }

        // 2) Gera plano de hoje (planner puro).
        var graph     = await _graphCache.GetOrBuildAsync(trailId, ct);
        var masteries = await _masteryRepo.GetByTrailAsync(userId, trailId, ct);
        var plan      = _planner.Plan(new JourneyPlanInput(
            UserId:         userId,
            Graph:          graph,
            Masteries:      masteries,
            LivesAvailable: lives,
            StreakDays:     streakDays,
            AsOf:           today));

        // 3) Penalidade — +1 desafio extra se não cumpriu (e havia plano).
        var extra = (metYesterday == false) ? 1 : 0;

        // 4) Persiste snapshot de hoje.
        var snap = new JourneySnapshot
        {
            UserId   = userId,
            TrailId  = trailId,
            PlanDate = today,
            MetaDia  = plan.MetaDia + extra,
            ExtraChallengesPenalty = extra,
            PlanJson = JsonSerializer.Serialize(plan),
            GeneratedAt = DateTime.UtcNow,
            MetGoal  = null,
        };
        await _snapshots.UpsertAsync(snap, ct);

        // 5) Notifica.
        await _eventBus.PublishAsync(new DailyPlanGenerated(
            userId, trailId, today, snap.MetaDia, extra, metYesterday), ct);

        return metYesterday;
    }
}

public sealed record DailyReplanReport(
    DateTime AsOf,
    int      Processed,
    int      Failures,
    int      YesterdayGoalMet);
