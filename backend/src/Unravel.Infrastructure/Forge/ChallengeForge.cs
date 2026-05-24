using Unravel.Application.Forge;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;

namespace Unravel.Infrastructure.Forge;

/// <summary>
/// Orquestrador do PR 4. Recebe N <see cref="IChallengeStrategy"/> via DI
/// (pronto pra plugar <c>LlmChallengeStrategy</c> futura), pede a cada uma
/// um lote de drafts, valida com <see cref="QualityGate"/>, calibra a saída
/// pela mastery do usuário (zona proximal) e devolve um pool curto.
///
/// <para><b>Calibragem</b>: filtra drafts cuja
/// <c>EstimatedDifficulty</c> está dentro de uma janela em torno do
/// <c>targetUserMastery + 0.15</c>. Drafts fora da janela só entram se o
/// pool ficar pequeno demais — preferimos servir algo a servir nada.</para>
/// </summary>
public sealed class ChallengeForge : IChallengeForge
{
    private readonly IEnumerable<IChallengeStrategy> _strategies;
    private readonly IKnowledgeGraphCache             _graphCache; // for topic lookup

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
        // 1. localiza o Topic correspondente ao Content (GraphBuilder usa
        //    Topic.Id == Content.Id por convenção, mas defensivo).
        var topic = graph.Topics.FirstOrDefault(t => t.ContentId == content.Id);
        if (topic is null) return Array.Empty<GeneratedChallengeDraft>();

        // 2. peça a cada estratégia ~2x o targetCount; o QualityGate vai
        //    derrubar uma parte e a calibragem outra.
        var per = Math.Max(2, targetCount);

        var raw = _strategies
            .SelectMany(s => s.Generate(content, topic, graph, per))
            .ToList();

        // 3. quality gate
        var approved = raw.Where(d => QualityGate.Approve(d, out _)).ToList();
        if (approved.Count == 0) return Array.Empty<GeneratedChallengeDraft>();

        // 4. calibragem — preferência por dificuldade próxima da target.
        //    Score = 1 - |diff - target|; tie-break por estratégia (rotação
        //    leve) e por hash determinístico do prompt.
        var target = Math.Clamp(targetUserMastery + 0.15, 0.10, 0.95);

        var ranked = approved
            .Select((d, idx) => new
            {
                Draft        = d,
                Fitness      = 1.0 - Math.Abs(d.EstimatedDifficulty - target),
                StrategyTier = (int)d.Strategy,
                Hash         = StableHash(d.Prompt),
                Idx          = idx,
            })
            .OrderByDescending(x => x.Fitness)
            .ThenBy(x => x.StrategyTier)        // rotação: Cloze antes de Definition antes de TF, etc.
            .ThenBy(x => x.Hash)
            .Select(x => x.Draft)
            .Take(targetCount)
            .ToList();

        return ranked;
    }

    private static int StableHash(string s)
    {
        // Hash determinístico (não depende do random seed do CLR).
        unchecked
        {
            var h = 23;
            foreach (var ch in s) h = h * 31 + ch;
            return h;
        }
    }
}
