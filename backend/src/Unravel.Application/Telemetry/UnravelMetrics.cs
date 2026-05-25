using System.Diagnostics.Metrics;

namespace Unravel.Application.Telemetry;

/// <summary>
/// Ponto único de instrumentação para todas as métricas do algoritmo do
/// Unravel. Usar como <c>UnravelMetrics.ForgeDraftsGenerated.Add(1, ...)</c>
/// nos use cases — exporters (Console/OTLP) configurados em
/// <c>Program.cs</c> coletam automaticamente via <see cref="MeterName"/>.
///
/// <para><b>Convenção de tags</b>: snake_case, valores estáveis (nada de
/// IDs ou strings com cardinalidade alta — vira pesadelo no Prometheus).
/// Strategy/Reason são enums; user_id/trail_id nunca viram tag.</para>
///
/// <para><b>Por que estático</b>: instrumentação não tem estado, lifecycle
/// é o do processo. DI singleton seria overhead sem ganho.</para>
/// </summary>
public static class UnravelMetrics
{
    public const string MeterName    = "Unravel.Algorithm";
    public const string MeterVersion = "1.0.0";

    private static readonly Meter Meter = new(MeterName, MeterVersion);

    // ── Forge (PR 4/5/17) ───────────────────────────────────────────

    /// <summary>Drafts produzidos pelas IChallengeStrategy. Tag: strategy.</summary>
    public static readonly Counter<long> ForgeDraftsGenerated =
        Meter.CreateCounter<long>(
            "unravel.forge.drafts_generated",
            unit: "drafts",
            description: "Drafts brutos produzidos pelas strategies (pré-QualityGate).");

    /// <summary>Drafts aprovados pelo QualityGate. Tag: strategy.</summary>
    public static readonly Counter<long> ForgeDraftsApproved =
        Meter.CreateCounter<long>(
            "unravel.forge.drafts_approved",
            unit: "drafts",
            description: "Drafts que passaram no QualityGate.");

    /// <summary>Drafts rejeitados pelo QualityGate. Tags: strategy, reason.</summary>
    public static readonly Counter<long> ForgeDraftsRejected =
        Meter.CreateCounter<long>(
            "unravel.forge.drafts_rejected",
            unit: "drafts",
            description: "Drafts rejeitados pelo QualityGate, com motivo.");

    /// <summary>Latência do Build inteiro. Tag: content_strategy_count.</summary>
    public static readonly Histogram<double> ForgeBuildDurationMs =
        Meter.CreateHistogram<double>(
            "unravel.forge.build_duration_ms",
            unit: "ms",
            description: "Latência do ChallengeForge.Build (todas strategies + gate + ranking).");

    // ── Submit do quiz (PR 13) ──────────────────────────────────────

    /// <summary>Cada submit. Tags: outcome=correct|wrong, strategy.</summary>
    public static readonly Counter<long> QuizSubmissions =
        Meter.CreateCounter<long>(
            "unravel.quiz.submissions",
            unit: "submissions",
            description: "Submissões de quiz processadas.");

    /// <summary>Latência total do submit (validação + mastery + recompensas).</summary>
    public static readonly Histogram<double> QuizSubmitDurationMs =
        Meter.CreateHistogram<double>(
            "unravel.quiz.submit_duration_ms",
            unit: "ms",
            description: "Latência do SubmitPoolChallengeUseCase ponta a ponta.");

    // ── Planner (PR 3) ──────────────────────────────────────────────

    /// <summary>metaDia calculado por plan. Histograma porque a distribuição
    /// importa (1 vs 8 são experiências muito diferentes).</summary>
    public static readonly Histogram<int> PlannerMetaDia =
        Meter.CreateHistogram<int>(
            "unravel.planner.meta_dia",
            unit: "items",
            description: "Distribuição de metaDia gerado pelo planner.");

    /// <summary>Itens incluídos por reason (NewLearning/DueReview/Reinforce).</summary>
    public static readonly Counter<long> PlannerItemsByReason =
        Meter.CreateCounter<long>(
            "unravel.planner.items_by_reason",
            unit: "items",
            description: "Items adicionados aos planos por categoria de razão.");

    // ── Cron diário (PR 7) ──────────────────────────────────────────

    public static readonly Histogram<double> CronRunDurationSec =
        Meter.CreateHistogram<double>(
            "unravel.cron.run_duration_sec",
            unit: "s",
            description: "Duração total de cada ciclo do DailyReplanService.");

    public static readonly Counter<long> CronTargetsProcessed =
        Meter.CreateCounter<long>(
            "unravel.cron.targets_processed",
            unit: "targets",
            description: "Targets (user, trilha) processados por ciclo.");

    public static readonly Counter<long> CronFailures =
        Meter.CreateCounter<long>(
            "unravel.cron.failures",
            unit: "failures",
            description: "Falhas isoladas em targets durante o ciclo.");

    public static readonly Counter<long> CronStreakResets =
        Meter.CreateCounter<long>(
            "unravel.cron.streak_resets",
            unit: "resets",
            description: "Quantos usuários tiveram streak resetado por inatividade.");

    public static readonly Counter<long> CronPenaltiesApplied =
        Meter.CreateCounter<long>(
            "unravel.cron.penalties_applied",
            unit: "penalties",
            description: "Quantos targets receberam +1 challenge de penalidade.");
}
