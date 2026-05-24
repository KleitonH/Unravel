using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Forge;
using Unravel.Infrastructure.Forge.Strategies;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Forge;

public class OrderingStrategyTests
{
    private static Content C(int id, string title, string body) =>
        new() { Id = id, TrailId = 1, Order = id, Title = title, Body = body, IsActive = true };

    private static (OrderingStrategy sut, KnowledgeGraph graph, Topic topic) Setup(string body)
    {
        var content  = C(1, "T", body);
        var builder  = new GraphBuilder(new RakeKeywordExtractor(), new DifficultyScorer());
        var graph    = builder.Build(1, new[] { content });
        var topic    = graph.Topics.First();
        var sut      = new OrderingStrategy(/* não usa picker */);
        return (sut, graph, topic);
    }

    [Fact]
    public void Generate_NumberedList_BuildsFourDistinctOptions()
    {
        var body = "Para o deploy de uma SPA Angular, siga:\n" +
                   "1. Rodar testes\n" +
                   "2. Gerar build de produção\n" +
                   "3. Fazer upload pro servidor estático\n" +
                   "4. Apontar o CDN para a nova versão";
        var (sut, graph, topic) = Setup(body);

        var drafts = sut.Generate(C(1, "Deploy", body), topic, graph, 1);
        Assert.Single(drafts);
        Assert.Equal(4, drafts[0].Options.Count);
        Assert.True(drafts[0].CorrectIndex is >= 0 and < 4);
        Assert.Equal(drafts[0].Options.Distinct().Count(), drafts[0].Options.Count);
    }

    [Fact]
    public void Generate_BulletList_AlsoMatches()
    {
        var body = "Fluxo de autenticação:\n" +
                   "- Usuário envia credenciais\n" +
                   "- Servidor valida\n" +
                   "- Servidor emite token JWT\n" +
                   "- Cliente armazena e envia em requests seguintes";
        var (sut, graph, topic) = Setup(body);

        var drafts = sut.Generate(C(1, "Auth", body), topic, graph, 1);
        Assert.Single(drafts);
    }

    [Fact]
    public void Generate_ListTooShortOrTooLong_ReturnsEmpty()
    {
        // 2 itens — abaixo do mínimo
        var shortBody = "1. Um\n2. Dois";
        var (sut1, g1, t1) = Setup(shortBody);
        Assert.Empty(sut1.Generate(C(1, "X", shortBody), t1, g1, 1));

        // 7 itens — acima do máximo
        var longBody = string.Join("\n", Enumerable.Range(1, 7).Select(i => $"{i}. Item {i}"));
        var (sut2, g2, t2) = Setup(longBody);
        Assert.Empty(sut2.Generate(C(1, "X", longBody), t2, g2, 1));
    }

    [Fact]
    public void Generate_NoListInBody_ReturnsEmpty()
    {
        var body = "Conteúdo puramente narrativo, sem listas estruturadas em formato detectável.";
        var (sut, graph, topic) = Setup(body);
        Assert.Empty(sut.Generate(C(1, "T", body), topic, graph, 1));
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var body = "1. Primeiro passo claro\n2. Segundo passo claro\n3. Terceiro passo claro";
        var (sut, graph, topic) = Setup(body);
        var a = sut.Generate(C(1, "T", body), topic, graph, 1);
        var b = sut.Generate(C(1, "T", body), topic, graph, 1);
        Assert.Equal(a[0].Prompt, b[0].Prompt);
        Assert.Equal(a[0].Options, b[0].Options);
        Assert.Equal(a[0].CorrectIndex, b[0].CorrectIndex);
    }
}
