using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;

namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// <see cref="ITopicResolver"/> baseado em similaridade lexical: extrai
/// keywords do texto do challenge via <see cref="IKeywordExtractor"/> e
/// calcula Jaccard contra cada <see cref="Topic"/> do grafo. Top-K pesados
/// proporcionalmente ao score.
///
/// <para>Quando nenhum tópico atinge similaridade mínima
/// (<see cref="MinSimilarity"/>), retorna lista vazia — preferimos
/// "não atualizar nada" a poluir o histórico de mastery com inferências
/// fracas. O hook do <c>ChallengeService</c> trata isso silenciosamente
/// (não bloqueia a submissão).</para>
/// </summary>
public sealed class KeywordTopicResolver : ITopicResolver
{
    private readonly IKeywordExtractor _extractor;

    /// <summary>Limiar mínimo de Jaccard para um tópico ser considerado
    /// "tocado" pelo challenge. 0.10 é generoso de propósito: o objetivo
    /// não é precisão cirúrgica, é nunca atualizar mastery de tópico
    /// claramente não relacionado. Calibrar p/ baixo se trilhas têm
    /// vocabulário muito repetido entre tópicos.</summary>
    public double MinSimilarity { get; init; } = 0.10;

    public KeywordTopicResolver(IKeywordExtractor extractor) => _extractor = extractor;

    public IReadOnlyList<TopicWeight> Resolve(string challengeText, KnowledgeGraph graph, int topK = 3)
    {
        if (string.IsNullOrWhiteSpace(challengeText) || graph.Topics.Count == 0)
            return Array.Empty<TopicWeight>();

        var challengeKeys = _extractor.Extract(challengeText, topN: 15)
            .SelectMany(k => k.Term.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(TextNormalizer.CanonicalKey))
            .Where(s => s.Length >= 2)
            .ToHashSet();

        if (challengeKeys.Count == 0) return Array.Empty<TopicWeight>();

        var scored = graph.Topics
            .Select(topic =>
            {
                var topicKeys = topic.Keywords
                    .SelectMany(k => k.Term.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(TextNormalizer.CanonicalKey))
                    .Where(s => s.Length >= 2)
                    .ToHashSet();

                if (topicKeys.Count == 0) return (topic.Id, jaccard: 0.0);

                var inter = challengeKeys.Intersect(topicKeys).Count();
                var union = challengeKeys.Count + topicKeys.Count - inter;
                return (topic.Id, jaccard: union == 0 ? 0.0 : (double)inter / union);
            })
            .Where(x => x.jaccard >= MinSimilarity)
            .OrderByDescending(x => x.jaccard)
            .ThenBy(x => x.Id)                                 // tie-break determinístico
            .Take(topK)
            .ToList();

        if (scored.Count == 0) return Array.Empty<TopicWeight>();

        var total = scored.Sum(x => x.jaccard);
        return scored.Select(x => new TopicWeight(x.Id, x.jaccard / total)).ToList();
    }
}
