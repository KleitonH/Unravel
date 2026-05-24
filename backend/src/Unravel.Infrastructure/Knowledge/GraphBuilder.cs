using Unravel.Application.Journey.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;

namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// Constrói um <see cref="KnowledgeGraph"/> determinístico a partir dos
/// Contents de uma trilha. Pipeline:
///
/// <list type="number">
///   <item>Extrai tópicos via <see cref="IKeywordExtractor"/> +
///   <see cref="DifficultyScorer"/>.</item>
///   <item>Para cada par (A, B), calcula <i>overlap</i> de keywords
///   (Jaccard sobre o conjunto canonizado).</item>
///   <item>Cria aresta A→B se overlap ≥ <c>OverlapThreshold</c>, B vem
///   depois de A na ordem original e <i>difficulty(B) ≥ difficulty(A)</i>.</item>
///   <item>Garante DAG: se sobrar ciclo (não deveria, dado o constraint
///   de ordem), quebra a aresta de menor peso.</item>
///   <item>Aplica <b>redução transitiva</b>: remove arestas redundantes
///   A→C quando já existe caminho A→B→C. Sem isso a UI mostra arestas
///   desnecessárias e o planner conta pré-requisitos duplicados.</item>
/// </list>
/// </summary>
public sealed class GraphBuilder : IKnowledgeGraphBuilder
{
    private readonly IKeywordExtractor _extractor;
    private readonly DifficultyScorer  _difficulty;

    /// <summary>Mínimo de Jaccard(keywords) para considerar aresta. 0.25 foi
    /// escolhido empiricamente: abaixo, surgem arestas-fantasma entre
    /// tópicos pouco relacionados; acima, perdemos pré-requisitos legítimos
    /// (a sobreposição de termos técnicos entre artigos correlatos é
    /// tipicamente 25–60%).</summary>
    public double OverlapThreshold { get; init; } = 0.25;

    public GraphBuilder(IKeywordExtractor extractor, DifficultyScorer difficulty)
    {
        _extractor  = extractor;
        _difficulty = difficulty;
    }

    public KnowledgeGraph Build(int trailId, IReadOnlyList<Content> contents)
    {
        var active = contents.Where(c => c.IsActive)
                             .OrderBy(c => c.Order)
                             .ThenBy(c => c.Id)
                             .ToList();

        var topics = active.Select((c, idx) =>
        {
            var keywords  = _extractor.Extract($"{c.Title}\n{c.Body}", topN: 12);
            var difficulty = _difficulty.Score(c.Title, c.Body, c.Level);
            var slug      = Slugify(c.Title);
            // Topic.Id = ContentId para 1:1, simplifica todo o resto.
            return new Topic(c.Id, c.Id, trailId, slug, keywords, difficulty, idx);
        }).ToList();

        var edges = InferEdges(topics);
        edges     = BreakCyclesIfAny(topics, edges);
        edges     = TransitiveReduction(topics, edges);

        return new KnowledgeGraph(trailId, topics, edges);
    }

    // ── Pipeline interno ─────────────────────────────────────────────

    private List<PrerequisiteEdge> InferEdges(List<Topic> topics)
    {
        var edges = new List<PrerequisiteEdge>();
        var keysByTopic = topics.ToDictionary(
            t => t.Id,
            t => t.Keywords.Select(k => CanonicalSet(k.Term)).Aggregate(
                new HashSet<string>(),
                (acc, parts) => { acc.UnionWith(parts); return acc; }));

        for (var i = 0; i < topics.Count; i++)
        for (var j = i + 1; j < topics.Count; j++)
        {
            var a = topics[i];
            var b = topics[j];

            // Pré-requisito implica que o "depois" seja >= em dificuldade.
            // Tolerância pequena: aceitamos b 0.05 abaixo de a (ruído).
            if (b.DifficultyScore + 0.05 < a.DifficultyScore) continue;

            var setA = keysByTopic[a.Id];
            var setB = keysByTopic[b.Id];
            if (setA.Count == 0 || setB.Count == 0) continue;

            var inter = setA.Intersect(setB).Count();
            var union = setA.Count + setB.Count - inter;
            var jaccard = (double)inter / union;

            if (jaccard >= OverlapThreshold)
                edges.Add(new PrerequisiteEdge(a.Id, b.Id, jaccard));
        }
        return edges;
    }

    private static List<PrerequisiteEdge> BreakCyclesIfAny(
        List<Topic> topics, List<PrerequisiteEdge> edges)
    {
        // Por construção, edges só vão de OriginalOrder menor → maior; impossível ciclar.
        // Mas mantemos o filtro defensivo: se algum invariante for relaxado no futuro,
        // este código continua sendo o "last line of defense".
        var orderById = topics.ToDictionary(t => t.Id, t => t.OriginalOrder);
        return edges.Where(e => orderById[e.FromTopicId] < orderById[e.ToTopicId])
                    .ToList();
    }

    private static List<PrerequisiteEdge> TransitiveReduction(
        List<Topic> topics, List<PrerequisiteEdge> edges)
    {
        // Para cada aresta (a, c), verifica se existe b com (a, b) e (b, c).
        // Se sim, (a, c) é redundante. O(|E| · |V|) — aceitável para trilhas
        // com dezenas/centenas de tópicos.
        var outgoing = edges.GroupBy(e => e.FromTopicId)
                            .ToDictionary(g => g.Key, g => g.Select(e => e.ToTopicId).ToHashSet());

        bool IsReachableExcludingDirect(int from, int to)
        {
            if (!outgoing.TryGetValue(from, out var directs)) return false;
            var visited = new HashSet<int>();
            var queue   = new Queue<int>();
            foreach (var mid in directs)
                if (mid != to) queue.Enqueue(mid);

            while (queue.TryDequeue(out var node))
            {
                if (!visited.Add(node)) continue;
                if (node == to) return true;
                if (outgoing.TryGetValue(node, out var nexts))
                    foreach (var n in nexts) queue.Enqueue(n);
            }
            return false;
        }

        return edges.Where(e => !IsReachableExcludingDirect(e.FromTopicId, e.ToTopicId))
                    .ToList();
    }

    private static IEnumerable<string> CanonicalSet(string term)
        => term.Split(' ', StringSplitOptions.RemoveEmptyEntries)
               .Select(TextNormalizer.CanonicalKey)
               .Where(s => s.Length >= 2);

    private static string Slugify(string s)
    {
        var folded = TextNormalizer.FoldDiacritics(s);
        var chars  = folded.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug   = new string(chars).Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Length > 60 ? slug[..60].TrimEnd('-') : slug;
    }
}
