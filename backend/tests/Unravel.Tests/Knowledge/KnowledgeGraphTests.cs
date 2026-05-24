using Unravel.Domain.Knowledge;

namespace Unravel.Tests.Knowledge;

public class KnowledgeGraphTests
{
    private static Topic T(int id, int order) =>
        new(id, contentId: id, trailId: 1, slug: $"t{id}",
            keywords: Array.Empty<Keyword>(),
            difficultyScore: 0.5,
            originalOrder: order);

    [Fact]
    public void GetPrerequisitesOf_ReturnsIncomingEdges()
    {
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1), T(3,2) },
            edges:  new[] { new PrerequisiteEdge(1, 3, 0.5), new PrerequisiteEdge(2, 3, 0.7) });

        var prereqs = graph.GetPrerequisitesOf(3);
        Assert.Equal(2, prereqs.Count);
        Assert.Contains(prereqs, e => e.FromTopicId == 1);
        Assert.Contains(prereqs, e => e.FromTopicId == 2);

        Assert.Empty(graph.GetPrerequisitesOf(1));
    }

    [Fact]
    public void GetUnlockedBy_ReturnsOutgoingEdges()
    {
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1), T(3,2) },
            edges:  new[] { new PrerequisiteEdge(1, 2, 0.5), new PrerequisiteEdge(1, 3, 0.7) });

        var unlocked = graph.GetUnlockedBy(1);
        Assert.Equal(2, unlocked.Count);
        Assert.Empty(graph.GetUnlockedBy(3));
    }

    [Fact]
    public void TopologicalOrder_RespectsEdges_AndIsStable()
    {
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(10,0), T(20,1), T(30,2), T(40,3) },
            edges:  new[]
            {
                new PrerequisiteEdge(10, 20, 0.5),
                new PrerequisiteEdge(10, 30, 0.5),
                new PrerequisiteEdge(20, 40, 0.5),
            });

        var order = graph.TopologicalOrder().Select(t => t.Id).ToList();
        Assert.Equal(4, order.Count);
        Assert.True(order.IndexOf(10) < order.IndexOf(20));
        Assert.True(order.IndexOf(10) < order.IndexOf(30));
        Assert.True(order.IndexOf(20) < order.IndexOf(40));

        var again = graph.TopologicalOrder().Select(t => t.Id).ToList();
        Assert.Equal(order, again);
    }

    [Fact]
    public void TopologicalOrder_DetectsCycleEvenThoughBuilderPreventsIt()
    {
        // Garantia defensiva: se um dia construirmos um grafo com ciclo (por bug),
        // queremos uma exceção clara em vez de loop/order parcial silencioso.
        var graph = new KnowledgeGraph(1,
            topics: new[] { T(1,0), T(2,1) },
            edges:  new[] { new PrerequisiteEdge(1, 2, 0.5), new PrerequisiteEdge(2, 1, 0.5) });

        Assert.Throws<InvalidOperationException>(() => graph.TopologicalOrder());
    }
}
