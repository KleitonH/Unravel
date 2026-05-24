using Unravel.Domain.Entities;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Knowledge;

public class GraphBuilderTests
{
    private readonly GraphBuilder _sut = new(new RakeKeywordExtractor(), new DifficultyScorer());

    private static Content C(int id, int order, string title, string body, DifficultyLevel level = DifficultyLevel.Beginner)
        => new() { Id = id, TrailId = 1, Order = order, Title = title, Body = body, Level = level, IsActive = true };

    [Fact]
    public void Build_EmptyTrail_ReturnsEmptyGraph()
    {
        var g = _sut.Build(1, Array.Empty<Content>());
        Assert.Empty(g.Topics);
        Assert.Empty(g.Edges);
    }

    [Fact]
    public void Build_IsAlwaysADag_TopologicalOrderSucceeds()
    {
        var contents = new List<Content>
        {
            C(1, 1, "Variáveis em JavaScript",     "Variáveis guardam valores. JavaScript tem let, const e var."),
            C(2, 2, "Funções em JavaScript",       "Funções em JavaScript encapsulam código. Usam variáveis e retorno.", DifficultyLevel.Intermediate),
            C(3, 3, "Closures em JavaScript",      "Closures são funções que capturam variáveis do escopo externo.",     DifficultyLevel.Advanced),
            C(4, 4, "Promises em JavaScript",      "Promises representam valores assíncronos. Usam funções de callback.", DifficultyLevel.Advanced),
        };

        var g = _sut.Build(1, contents);

        Assert.Equal(4, g.Topics.Count);
        var order = g.TopologicalOrder();
        Assert.Equal(4, order.Count);

        // posições no order devem respeitar todas as arestas
        var pos = order.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);
        foreach (var e in g.Edges)
            Assert.True(pos[e.FromTopicId] < pos[e.ToTopicId],
                $"edge {e.FromTopicId}→{e.ToTopicId} violates topological order");
    }

    [Fact]
    public void Build_EdgesGoFromLowerToHigherOriginalOrder()
    {
        var contents = new List<Content>
        {
            C(10, 1, "Modelagem Relacional",  "Modelagem de tabelas, chaves primárias e estrangeiras."),
            C(20, 2, "SQL Básico",            "SELECT, INSERT, UPDATE, DELETE em tabelas.",                DifficultyLevel.Beginner),
            C(30, 3, "Joins em SQL",          "INNER JOIN, LEFT JOIN sobre tabelas relacionais com SELECT.", DifficultyLevel.Intermediate),
        };

        var g = _sut.Build(1, contents);
        Assert.All(g.Edges, e =>
        {
            var from = g.GetTopic(e.FromTopicId)!;
            var to   = g.GetTopic(e.ToTopicId)!;
            Assert.True(from.OriginalOrder < to.OriginalOrder);
        });
    }

    [Fact]
    public void Build_AppliesTransitiveReduction()
    {
        // Três Contents fortemente sobrepostos: a chance de virem A→B, B→C e A→C é alta.
        // Após redução transitiva, A→C deve sumir.
        var contents = new List<Content>
        {
            C(1, 1, "Banco de Dados",                  "Banco de dados relacional armazena dados em tabelas."),
            C(2, 2, "Banco de Dados Avançado",         "Banco de dados relacional avançado usa índices e transações em tabelas.", DifficultyLevel.Intermediate),
            C(3, 3, "Banco de Dados Distribuído",      "Banco de dados distribuído escala usando réplicas e particionamento em tabelas.", DifficultyLevel.Advanced),
        };

        var g = _sut.Build(1, contents);
        var hasAtoB = g.Edges.Any(e => e.FromTopicId == 1 && e.ToTopicId == 2);
        var hasBtoC = g.Edges.Any(e => e.FromTopicId == 2 && e.ToTopicId == 3);
        var hasAtoC = g.Edges.Any(e => e.FromTopicId == 1 && e.ToTopicId == 3);

        // Se houver A→B E B→C, então A→C deve ter sido removida.
        if (hasAtoB && hasBtoC)
            Assert.False(hasAtoC, "transitive reduction should drop A→C when A→B→C exists");
    }

    [Fact]
    public void Build_DeterministicTopologicalOrder()
    {
        var contents = new List<Content>
        {
            C(1, 1, "HTML",       "Estrutura básica de uma página com tags."),
            C(2, 2, "CSS",        "Estilização de páginas HTML com seletores."),
            C(3, 3, "JavaScript", "Linguagem que adiciona comportamento a páginas HTML.", DifficultyLevel.Intermediate),
        };

        var a = _sut.Build(1, contents).TopologicalOrder().Select(t => t.Id).ToList();
        var b = _sut.Build(1, contents).TopologicalOrder().Select(t => t.Id).ToList();
        Assert.Equal(a, b);
    }
}
