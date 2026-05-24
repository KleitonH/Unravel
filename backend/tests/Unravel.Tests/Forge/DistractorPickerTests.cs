using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Forge;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Forge;

public class DistractorPickerTests
{
    private readonly GraphBuilder _builder = new(new RakeKeywordExtractor(), new DifficultyScorer());
    private readonly DistractorPicker _sut = new();

    private static Content C(int id, int order, string title, string body) =>
        new() { Id = id, TrailId = 1, Order = order, Title = title, Body = body, IsActive = true };

    private KnowledgeGraph Graph() => _builder.Build(1, new List<Content>
    {
        C(1, 1, "Hexagonal",  "Arquitetura hexagonal isola domínio de infraestrutura via ports e adapters."),
        C(2, 2, "MVC",        "MVC separa apresentação, modelo e controlador."),
        C(3, 3, "Layered",    "Arquitetura em camadas organiza código em camadas horizontais."),
        C(4, 4, "Microservices", "Microservices fragmentam o sistema em serviços independentes."),
    });

    [Fact]
    public void Pick_ReturnsTermsFromOtherTopicsOnly()
    {
        var graph = Graph();
        var sourceTopic = graph.Topics.First(t => t.ContentId == 1);

        var distractors = _sut.Pick("Hexagonal", sourceTopic, graph, count: 3);

        Assert.True(distractors.Count <= 3);
        Assert.DoesNotContain("Hexagonal", distractors, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pick_EmptyInput_ReturnsEmpty()
    {
        var graph = Graph();
        var topic = graph.Topics.First();
        Assert.Empty(_sut.Pick("", topic, graph, 3));
        Assert.Empty(_sut.Pick("Hexagonal", topic, graph, 0));
    }

    [Fact]
    public void Pick_IsDeterministic()
    {
        var graph = Graph();
        var topic = graph.Topics.First(t => t.ContentId == 1);

        var a = _sut.Pick("Hexagonal", topic, graph, 3);
        var b = _sut.Pick("Hexagonal", topic, graph, 3);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Pick_AllUppercaseReference_MatchesUppercase()
    {
        var graph = Graph();
        var topic = graph.Topics.First(t => t.ContentId == 1);

        var distractors = _sut.Pick("HEXAGONAL", topic, graph, 3);

        Assert.All(distractors, d =>
            Assert.True(d.All(c => !char.IsLetter(c) || char.IsUpper(c)),
                       $"esperava maiúsculas, obteve '{d}'"));
    }
}
