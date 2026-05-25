using Unravel.Application.Forge;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;

namespace Unravel.Infrastructure.Forge;

/// <summary>
/// Orquestrador do Forge. Recebe N <see cref="IChallengeStrategy"/> via DI
/// (pronto pra plugar <c>LlmChallengeStrategy</c> futura), pede a cada uma
/// um lote de drafts, valida com <see cref="QualityGate"/>, calibra a saída
/// pela mastery do usuário (zona proximal) e devolve um pool curto.
///
/// <para><b>Calibragem</b>: prefere drafts cuja <c>EstimatedDifficulty</c>
/// está próxima de <c>targetUserMastery + 0.15</c> (zona proximal).</para>
///
/// <para><b>Diversidade (PR 17)</b>: a ordenação pura por fitness fazia a
/// estratégia mais prolífica (geralmente Cloze) saturar o top-N e a UX virar
/// monótona. Agora aplicamos um passo de diversificação: garantimos até
/// <see cref="MinDistinctStrategies"/> estratégias distintas no resultado
/// sempre que houver drafts dessas estratégias disponíveis. O ranking
/// continua determinístico: mesmo input → mesma saída e ordem.</para>
/// </summary>
public sealed class ChallengeForge : IChallengeForge
{
    private readonly IEnumerable<IChallengeStrategy> _strategies;
    private readonly IKnowledgeGraphCache             _graphCache; // for topic lookup

    /// <summary>Quantas estratégias distintas tentamos garantir no pool
    /// quando há drafts suficientes. 3 espelha o pool padrão de targetCount=5
    /// (60% diversificado, 40% por fitness puro).</summary>
    public int MinDistinctStrategies { get; init; } = 3;

    public ChallengeForge(
        IEnumerable<IChallengeStrategy> strategies,
        IKnowledgeGraphCache graphCache)
    {
        _strategies = strategies;
        _graphCache = graphCache;
    }

    public IReadOnlyList<GeneratedChallengeDraft> Build(
        Content content,
        KnowledgeGraph graph,
        int targetCount,
        double targetUserMastery = 0.3)
    {
        var topic = graph.Topics.FirstOrDefault(t => t.ContentId == content.Id);
        if (topic is null) return Array.Empty<GeneratedChallengeDraft>();

        var per = Math.Max(2, targetCount);

        var raw = _strategies
            .SelectMany(s => s.Generate(content, topic, graph, per))
            .ToList();

        var approved = raw.Where(d => QualityGate.Approve(d, out _)).ToList();
        if (approved.Count == 0) return Array.Empty<GeneratedChallengeDraft>();

        var target = Math.Clamp(targetUserMastery + 0.15, 0.10, 0.95);

        // Ranking primário — por fitness, com tie-break determinístico.
        var ranked = approved
            .Select(d => new ScoredDraft(d, Fitness(d, target), StableHash(d.Prompt)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => (int)x.Draft.Strategy)
            .ThenBy(x => x.Hash)
            .ToList();

        return Diversify(ranked, targetCount, MinDistinctStrategies);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static double Fitness(GeneratedChallengeDraft d, double target)
        => 1.0 - Math.Abs(d.EstimatedDifficulty - target);

    /// <summary>
    /// Garante até <paramref name="minDistinct"/> estratégias distintas no
    /// top-<paramref name="targetCount"/>. Algoritmo:
    /// <list type="number">
    ///   <item>Pega top-N por fitness (ordem original).</item>
    ///   <item>Conta estratégias distintas presentes.</item>
    ///   <item>Para cada estratégia ausente que tem ao menos 1 draft
    ///   aprovado: substitui o draft de pior fitness do top-N (cuja
    ///   estratégia esteja sobre-representada) pelo melhor draft da
    ///   estratégia ausente.</item>
    ///   <item>Reordena por fitness antes de retornar (manter UX consistente
    ///   com "melhores primeiro").</item>
    /// </list>
    /// Determinístico — todas as escolhas dependem só do ranking.
    /// </summary>
    private static IReadOnlyList<GeneratedChallengeDraft> Diversify(
        List<ScoredDraft> ranked, int targetCount, int minDistinct)
    {
        if (ranked.Count == 0 || targetCount <= 0) return Array.Empty<GeneratedChallengeDraft>();

        var top = ranked.Take(targetCount).ToList();
        if (minDistinct <= 1) return top.Select(x => x.Draft).ToList();

        var availableStrategies = ranked
            .Select(x => x.Draft.Strategy).Distinct().Count();
        var goalDistinct = Math.Min(minDistinct, Math.Min(availableStrategies, targetCount));

        while (top.Select(x => x.Draft.Strategy).Distinct().Count() < goalDistinct)
        {
            var present = top.Select(x => x.Draft.Strategy).ToHashSet();

            // Próxima estratégia ausente, escolhida pelo melhor candidato
            // disponível fora do top (estabilidade: a estratégia com o
            // melhor candidato vence).
            var bestMissing = ranked
                .Where(x => !present.Contains(x.Draft.Strategy))
                .FirstOrDefault();

            if (bestMissing is null) break; // nada a fazer

            // Slot a substituir: o draft de pior fitness no top cuja
            // estratégia está duplicada — assim removemos redundância,
            // não diversidade.
            var slotToSwap = top
                .Where(x => top.Count(y => y.Draft.Strategy == x.Draft.Strategy) > 1)
                .OrderBy(x => x.Score)
                .ThenByDescending(x => x.Hash)
                .FirstOrDefault();

            if (slotToSwap is null) break;   // sem duplicatas; não há o que substituir

            top.Remove(slotToSwap);
            top.Add(bestMissing);
        }

        // Reordena pra UX coerente: melhor fitness primeiro.
        return top
            .OrderByDescending(x => x.Score)
            .ThenBy(x => (int)x.Draft.Strategy)
            .ThenBy(x => x.Hash)
            .Select(x => x.Draft)
            .ToList();
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            var h = 23;
            foreach (var ch in s) h = h * 31 + ch;
            return h;
        }
    }

    private sealed record ScoredDraft(GeneratedChallengeDraft Draft, double Score, int Hash);
}
