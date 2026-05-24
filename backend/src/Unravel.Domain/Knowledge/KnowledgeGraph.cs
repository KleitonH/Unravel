namespace Unravel.Domain.Knowledge;

/// <summary>
/// Grafo dirigido acíclico (DAG) que organiza os tópicos de uma trilha por
/// pré-requisitos inferidos. Estrutura imutável; reconstruída pelo
/// <c>GraphBuilder</c> quando a trilha muda. Todos os métodos de consulta são
/// O(1) ou O(degree) e seguros para uso concorrente — não há mutação após
/// construção.
/// </summary>
public sealed class KnowledgeGraph
{
    public int TrailId { get; }

    private readonly IReadOnlyDictionary<int, Topic> _topicsById;
    private readonly IReadOnlyList<PrerequisiteEdge> _edges;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<PrerequisiteEdge>> _incoming;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<PrerequisiteEdge>> _outgoing;

    public IReadOnlyCollection<Topic>            Topics => (IReadOnlyCollection<Topic>)_topicsById.Values;
    public IReadOnlyList<PrerequisiteEdge>       Edges  => _edges;

    public KnowledgeGraph(int trailId, IEnumerable<Topic> topics, IEnumerable<PrerequisiteEdge> edges)
    {
        TrailId     = trailId;
        _topicsById = topics.ToDictionary(t => t.Id);
        _edges      = edges.ToList();

        _incoming = _edges
            .GroupBy(e => e.ToTopicId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PrerequisiteEdge>)g.ToList());

        _outgoing = _edges
            .GroupBy(e => e.FromTopicId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PrerequisiteEdge>)g.ToList());
    }

    public Topic? GetTopic(int topicId)
        => _topicsById.TryGetValue(topicId, out var t) ? t : null;

    /// <summary>Arestas que apontam <i>para</i> o tópico — ou seja, os
    /// pré-requisitos diretos. Vazio se o tópico é uma "raiz".</summary>
    public IReadOnlyList<PrerequisiteEdge> GetPrerequisitesOf(int topicId)
        => _incoming.TryGetValue(topicId, out var list) ? list : Array.Empty<PrerequisiteEdge>();

    /// <summary>Arestas que <i>saem</i> do tópico — o que ele desbloqueia
    /// quando dominado. Usado pelo planner para priorizar nós que destravam
    /// mais coisas.</summary>
    public IReadOnlyList<PrerequisiteEdge> GetUnlockedBy(int topicId)
        => _outgoing.TryGetValue(topicId, out var list) ? list : Array.Empty<PrerequisiteEdge>();

    /// <summary>Ordem topológica determinística (Kahn) — útil para seedar a
    /// jornada inicial de um usuário novo, sequência de revisão e como
    /// fallback do planner antes de qualquer mastery existir.</summary>
    public IReadOnlyList<Topic> TopologicalOrder()
    {
        var indegree = _topicsById.Keys.ToDictionary(id => id, id => GetPrerequisitesOf(id).Count);
        // ordenar por (indegree=0, OriginalOrder) — determinismo total
        var queue = new PriorityQueue<int, (int order, int id)>();
        foreach (var (id, deg) in indegree)
            if (deg == 0) queue.Enqueue(id, (_topicsById[id].OriginalOrder, id));

        var result = new List<Topic>(_topicsById.Count);
        while (queue.TryDequeue(out var id, out _))
        {
            result.Add(_topicsById[id]);
            foreach (var edge in GetUnlockedBy(id))
            {
                if (--indegree[edge.ToTopicId] == 0)
                {
                    var t = _topicsById[edge.ToTopicId];
                    queue.Enqueue(edge.ToTopicId, (t.OriginalOrder, t.Id));
                }
            }
        }

        // Se sobrar algo, há ciclo — não deveria, mas defensivo.
        if (result.Count != _topicsById.Count)
            throw new InvalidOperationException(
                $"Cycle detected in KnowledgeGraph(trailId={TrailId}). " +
                $"GraphBuilder should have broken it.");

        return result;
    }
}
