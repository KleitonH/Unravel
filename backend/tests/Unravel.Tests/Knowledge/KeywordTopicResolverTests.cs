using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Knowledge;

public class KeywordTopicResolverTests
{
    private readonly GraphBuilder _builder = new(new RakeKeywordExtractor(), new DifficultyScorer());
    private readonly KeywordTopicResolver _sut = new(new RakeKeywordExtractor());

    private static Content C(int id, int order, string title, string body, DifficultyLevel level = DifficultyLevel.Beginner)
        => new() { Id = id, TrailId = 1, Order = order, Title = title, Body = body, Level = level, IsActive = true };

    private KnowledgeGraph TrailWithThreeTopics() => _builder.Build(1, new List<Content>
    {
        C(1, 1, "Joins em SQL",        "INNER JOIN, LEFT JOIN combinam linhas de tabelas em SQL."),
        C(2, 2, "Componentes Angular", "Componentes Angular encapsulam template, estilo e lógica TypeScript."),
        C(3, 3, "Closures JavaScript", "Closures em JavaScript capturam variáveis do escopo léxico externo."),
    });

    [Fact]
    public void Resolve_PicksTopicByKeywordOverlap()
    {
        var graph  = TrailWithThreeTopics();
        var topics = _sut.Resolve("O que faz um INNER JOIN entre duas tabelas em SQL?", graph);

        Assert.NotEmpty(topics);
        Assert.Equal(1, topics[0].TopicId);  // tópico de Joins SQL
    }

    [Fact]
    public void Resolve_WeightsSumToApproximatelyOne()
    {
        var graph  = TrailWithThreeTopics();
        var topics = _sut.Resolve(
            "Como funcionam closures e componentes encapsulando lógica em JavaScript?",
            graph);

        if (topics.Count > 0)
            Assert.Equal(1.0, topics.Sum(t => t.Weight), precision: 6);
    }

    [Fact]
    public void Resolve_NoRelatedTopic_ReturnsEmpty()
    {
        var graph  = TrailWithThreeTopics();
        var topics = _sut.Resolve(
            "Manjericão fresco vai bem com molho de tomate ao pesto.",
            graph);

        Assert.Empty(topics);
    }

    [Fact]
    public void Resolve_EmptyText_ReturnsEmpty()
    {
        var graph = TrailWithThreeTopics();
        Assert.Empty(_sut.Resolve("", graph));
        Assert.Empty(_sut.Resolve("   ", graph));
        Assert.Empty(_sut.Resolve(null!, graph));
    }

    [Fact]
    public void Resolve_EmptyGraph_ReturnsEmpty()
    {
        var emptyGraph = _builder.Build(1, Array.Empty<Content>());
        Assert.Empty(_sut.Resolve("qualquer coisa", emptyGraph));
    }

    [Fact]
    public void Resolve_IsDeterministic()
    {
        var graph  = TrailWithThreeTopics();
        var text   = "Como escrever um JOIN entre tabelas em SQL?";
        var a = _sut.Resolve(text, graph);
        var b = _sut.Resolve(text, graph);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].TopicId, b[i].TopicId);
            Assert.Equal(a[i].Weight, b[i].Weight, precision: 10);
        }
    }

    [Fact]
    public void Resolve_RespectsTopK()
    {
        var graph  = TrailWithThreeTopics();
        var topics = _sut.Resolve(
            "tabelas SQL e componentes em Angular usando TypeScript com closures",
            graph, topK: 2);

        Assert.True(topics.Count <= 2);
    }
}
