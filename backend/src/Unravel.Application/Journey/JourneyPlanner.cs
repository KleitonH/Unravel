using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey;

/// <summary>
/// Implementação do <see cref="IJourneyPlanner"/>. Algoritmo determinístico,
/// puro, in-process — sem BD, sem clock interno, sem LLM.
///
/// <para><b>Pipeline:</b></para>
/// <list type="number">
///   <item>Indexa masteries por TopicId e calcula <c>effectiveMastery</c>
///   (com decaimento) para cada tópico do grafo.</item>
///   <item>Filtra candidatos: <c>effectiveMastery &lt; threshold</c> OU
///   revisão SRS vencida.</item>
///   <item>Aplica gating de pré-requisitos: candidato só passa se TODOS
///   os pré-requisitos diretos têm <c>effectiveMastery ≥ threshold</c>.
///   Cold-start (nenhuma mastery existe) → só raízes do DAG passam.</item>
///   <item>Pontua cada candidato (ver <see cref="Score"/>).</item>
///   <item>Calcula <c>metaDia</c> e separa <c>Today</c>/<c>Upcoming</c>
///   por ordem de score.</item>
/// </list>
///
/// <para><b>Por que pure</b>: o planner é a regra mais sensível do produto.
/// Mantê-lo puro (sem clock, sem DB) significa que cada decisão pode ser
/// reproduzida em teste com inputs sintéticos, auditada em produção
/// (snapshot do <see cref="JourneyPlanInput"/>) e re-executada em batch
/// no cron diário sem nenhum mock.</para>
/// </summary>
public sealed class JourneyPlanner : IJourneyPlanner
{
    public JourneyPlan Plan(JourneyPlanInput input)
    {
        var opts = input.Options ?? JourneyPlannerOptions.Default;
        var masteryByTopic = input.Masteries.ToDictionary(m => m.TopicId);

        // 1. effective mastery para todo tópico do grafo (ausente = 0).
        var effective = input.Graph.Topics.ToDictionary(
            t => t.Id,
            t => masteryByTopic.TryGetValue(t.Id, out var m)
                ? MasteryScoring.EffectiveScore(m, input.AsOf)
                : 0.0);

        // 2 + 3. candidatos com gating de pré-requisitos.
        var candidates = input.Graph.Topics.Where(t =>
        {
            var eff = effective[t.Id];
            var hasMastery = masteryByTopic.TryGetValue(t.Id, out var m);
            var isDue = hasMastery && MasteryScoring.IsDueForReview(m!, input.AsOf);

            // Não-candidatos: já dominado E sem revisão vencida.
            if (eff >= opts.MasteryThreshold && !isDue) return false;

            // Gating: todos pré-requisitos devem estar dominados.
            // Comparamos contra threshold-ε para evitar empate cego.
            return input.Graph.GetPrerequisitesOf(t.Id)
                .All(edge => effective[edge.FromTopicId] >= opts.MasteryThreshold - 1e-9);
        }).ToList();

        if (candidates.Count == 0)
            return new JourneyPlan(input.UserId, input.Graph.TrailId, input.AsOf,
                MetaDia: 0, Today: Array.Empty<JourneyItem>(), Upcoming: Array.Empty<JourneyItem>());

        // 4. pontua e ordena.
        var userLevel = EstimateUserLevel(input.Graph, effective);
        var ranked = candidates.Select(t =>
        {
            var hasMastery = masteryByTopic.TryGetValue(t.Id, out var m);
            var eff        = effective[t.Id];
            var reason     = !hasMastery
                ? JourneyReason.NewLearning
                : MasteryScoring.IsDueForReview(m!, input.AsOf)
                    ? JourneyReason.DueReview
                    : JourneyReason.Reinforce;

            var priority = Score(t, eff, m, input.AsOf, input.Graph, userLevel, opts);

            return new JourneyItem(
                TopicId:          t.Id,
                ContentId:        t.ContentId,
                Slug:             t.Slug,
                Priority:         priority,
                Reason:           reason,
                EffectiveMastery: eff,
                DifficultyScore:  t.DifficultyScore);
        })
        .OrderByDescending(i => i.Priority)
        .ThenBy(i => i.TopicId)                            // tie-break determinístico
        .ToList();

        // 5. metaDia e split.
        var metaDia    = ComputeMetaDia(input.LivesAvailable, input.StreakDays,
                                        candidatesCount: ranked.Count, opts);
        var today      = ranked.Take(metaDia).ToList();
        var upcomingN  = Math.Min(opts.UpcomingDays, ranked.Count - metaDia);
        var upcoming   = ranked.Skip(metaDia).Take(upcomingN).ToList();

        return new JourneyPlan(input.UserId, input.Graph.TrailId, input.AsOf,
            metaDia, today, upcoming);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>Função de score combinando os 4 sinais:
    /// <list type="bullet">
    ///   <item><b>need</b> (1 - eff): quanto pior o domínio, maior a urgência.</item>
    ///   <item><b>unlock_density</b>: fanout no DAG normalizado pelo maior
    ///   fanout do grafo. Tópicos que destravam mais coisa sobem.</item>
    ///   <item><b>srs_overdue</b>: dias atrasados / 14, clamp [0,1]. Só
    ///   conta se há mastery (cold-start não tem SRS).</item>
    ///   <item><b>difficulty_fit</b> (zona proximal): 1 - (difficulty - userLevel)².
    ///   Pico em difficulty ≈ userLevel; cai com o quadrado da distância.</item>
    /// </list>
    /// </summary>
    private static double Score(
        Topic topic, double effective, Mastery? mastery, DateTime asOf,
        KnowledgeGraph graph, double userLevel, JourneyPlannerOptions opts)
    {
        var need = 1.0 - effective;

        var fanout       = graph.GetUnlockedBy(topic.Id).Count;
        var maxFanout    = graph.Topics.Max(t => graph.GetUnlockedBy(t.Id).Count);
        var unlockNorm   = maxFanout == 0 ? 0 : (double)fanout / maxFanout;

        var srsOverdue = 0.0;
        if (mastery is not null && MasteryScoring.IsDueForReview(mastery, asOf))
        {
            var daysLate = (asOf - mastery.NextDueAt).TotalDays;
            srsOverdue   = Math.Clamp(daysLate / 14.0, 0, 1);
        }

        var diff       = topic.DifficultyScore - userLevel;
        var fit        = Math.Clamp(1.0 - diff * diff, 0, 1);

        return opts.WeightNeed          * need
             + opts.WeightUnlock        * unlockNorm
             + opts.WeightSrsOverdue    * srsOverdue
             + opts.WeightDifficultyFit * fit;
    }

    /// <summary>Estima o "nível" do usuário na trilha como média do
    /// effective mastery dos tópicos já tocados, pesada pela confiança.
    /// Cold-start (nenhum mastery) → 0.2 (assumimos beginner suave para
    /// que a zona proximal puxe tópicos fáceis).</summary>
    private static double EstimateUserLevel(KnowledgeGraph graph, Dictionary<int, double> effective)
    {
        var touched = effective.Where(kv => kv.Value > 0).ToList();
        if (touched.Count == 0) return 0.2;
        return touched.Average(kv => kv.Value);
    }

    /// <summary>
    /// <para>Vidas e streak modulam <c>metaDia</c>:</para>
    /// <list type="bullet">
    ///   <item>Cada vida sustenta ~1.5 tentativas (uma vida cobre ~2/3 dos
    ///   challenges com erro de cálculo).</item>
    ///   <item>Streak adiciona 1 ao cap a cada 7 dias (recompensa
    ///   consistência sem virar maratona).</item>
    /// </list>
    /// Cap [MinMetaDia, MaxMetaDia] e cap por candidatos disponíveis —
    /// nunca prometemos mais conteúdo do que existe.
    /// </summary>
    private static int ComputeMetaDia(int lives, int streak, int candidatesCount, JourneyPlannerOptions opts)
    {
        if (candidatesCount == 0) return 0;
        var lifeCap   = (int)Math.Floor(lives * 1.5);
        var streakCap = 3 + streak / 7;
        var raw       = Math.Min(lifeCap, streakCap);
        var bounded   = Math.Clamp(raw, opts.MinMetaDia, opts.MaxMetaDia);
        return Math.Min(bounded, candidatesCount);
    }
}
