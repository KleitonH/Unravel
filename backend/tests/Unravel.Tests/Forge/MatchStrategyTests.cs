using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Forge;
using Unravel.Infrastructure.Forge.Strategies;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Forge;

public class MatchStrategyTests
{
    private static Content C(int id, string title, string body) =>
        new() { Id = id, TrailId = 1, Order = id, Title = title, Body = body, IsActive = true };

    private static (MatchStrategy sut, KnowledgeGraph graph, Topic topic) Setup(string body)
    {
        var content  = C(1, "T", body);
        var builder  = new GraphBuilder(new RakeKeywordExtractor(), new DifficultyScorer());
        var graph    = builder.Build(1, new[] { content });
        var topic    = graph.Topics.First();
        var sut      = new MatchStrategy();
        return (sut, graph, topic);
    }

    [Fact]
    public void Generate_ColonPairs_ProducesAssociationQuestion()
    {
        var body = "Conceitos fundamentais:\n" +
                   "GET: leitura de recursos sem efeitos colaterais\n" +
                   "POST: criação de recurso novo\n" +
                   "PUT: substituição completa de recurso existente\n" +
                   "DELETE: remoção de recurso";
        var (sut, graph, topic) = Setup(body);

        var drafts = sut.Generate(C(1, "REST", body), topic, graph, 1);
        Assert.Single(drafts);
        Assert.Equal(4, drafts[0].Options.Count);
        Assert.Contains("GET", drafts[0].Options[drafts[0].CorrectIndex]);
        Assert.Contains("leitura", drafts[0].Options[drafts[0].CorrectIndex]);
    }

    [Fact]
    public void Generate_DashPairs_AlsoMatches()
    {
        var body = "Tipos de testes:\n" +
                   "Unitário — testa componentes isolados sem dependências externas\n" +
                   "Integração — testa cooperação entre módulos com infraestrutura\n" +
                   "End-to-end — testa fluxo completo do usuário";
        var (sut, graph, topic) = Setup(body);

        var drafts = sut.Generate(C(1, "Testes", body), topic, graph, 1);
        Assert.Single(drafts);
    }

    [Fact]
    public void Generate_TooFewPairs_ReturnsEmpty()
    {
        var body = "Apenas dois conceitos:\n" +
                   "Foo: definição um e definição\n" +
                   "Bar: definição dois e definição";
        var (sut, graph, topic) = Setup(body);
        Assert.Empty(sut.Generate(C(1, "X", body), topic, graph, 1));
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var body = "Métodos HTTP:\n" +
                   "GET: leitura de recursos sem efeitos colaterais\n" +
                   "POST: criação de recurso novo no servidor\n" +
                   "DELETE: remoção de recurso existente do servidor";
        var (sut, graph, topic) = Setup(body);
        var a = sut.Generate(C(1, "REST", body), topic, graph, 1);
        var b = sut.Generate(C(1, "REST", body), topic, graph, 1);
        Assert.Equal(a[0].Options, b[0].Options);
        Assert.Equal(a[0].CorrectIndex, b[0].CorrectIndex);
    }
}
