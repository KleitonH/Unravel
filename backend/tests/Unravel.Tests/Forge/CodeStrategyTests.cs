using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Forge;
using Unravel.Infrastructure.Forge.Strategies;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Forge;

public class CodeStrategyTests
{
    private static Content C(int id, string title, string body) =>
        new() { Id = id, TrailId = 1, Order = id, Title = title, Body = body, IsActive = true };

    private static (CodeStrategy sut, KnowledgeGraph graph, Topic topic) Setup(string body)
    {
        var content  = C(1, "Code", body);
        var builder  = new GraphBuilder(new RakeKeywordExtractor(), new DifficultyScorer());
        var graph    = builder.Build(1, new[] { content });
        var topic    = graph.Topics.First();
        var sut      = new CodeStrategy();
        return (sut, graph, topic);
    }

    [Fact]
    public void Generate_JsConsoleLogString_ProducesQuestion()
    {
        var body = "Exemplo simples em JavaScript:\n\n" +
                   "```js\nconsole.log(\"olá mundo\");\n```";
        var (sut, graph, topic) = Setup(body);

        var drafts = sut.Generate(C(1, "JS Hello", body), topic, graph, 1);
        Assert.Single(drafts);
        Assert.Contains("olá mundo", drafts[0].Options[drafts[0].CorrectIndex]);
        Assert.True(drafts[0].Options.Count is >= 3 and <= 4);
        Assert.Contains("JavaScript", drafts[0].Prompt);
    }

    [Fact]
    public void Generate_JsConsoleLogNumber_NeighboringDistractors()
    {
        var body = "```javascript\nconsole.log(42);\n```";
        var (sut, graph, topic) = Setup(body);

        var drafts = sut.Generate(C(1, "Num", body), topic, graph, 1);
        Assert.Single(drafts);
        var opts = drafts[0].Options;
        Assert.Contains("42", opts);
        Assert.Contains("43", opts);    // n+1
    }

    [Fact]
    public void Generate_PythonPrint_AlsoMatches()
    {
        var body = "Exemplo Python:\n\n" +
                   "```python\nprint(\"oi\")\n```";
        var (sut, graph, topic) = Setup(body);

        var drafts = sut.Generate(C(1, "PY", body), topic, graph, 1);
        Assert.Single(drafts);
        Assert.Contains("Python", drafts[0].Prompt);
    }

    [Fact]
    public void Generate_BoolLiteral_GivesComplement()
    {
        var body = "```js\nconsole.log(true);\n```";
        var (sut, graph, topic) = Setup(body);

        var drafts = sut.Generate(C(1, "B", body), topic, graph, 1);
        Assert.Single(drafts);
        Assert.Contains("true", drafts[0].Options);
        Assert.Contains("false", drafts[0].Options);
    }

    [Fact]
    public void Generate_NoCodeFence_ReturnsEmpty()
    {
        var body = "Texto sem blocos de código formatados — apenas prosa.";
        var (sut, graph, topic) = Setup(body);
        Assert.Empty(sut.Generate(C(1, "X", body), topic, graph, 1));
    }

    [Fact]
    public void Generate_UnsupportedLanguage_ReturnsEmpty()
    {
        // C# fora do escopo desta estratégia (intencional: o parser é
        // deliberadamente estreito).
        var body = "```csharp\nConsole.WriteLine(\"x\");\n```";
        var (sut, graph, topic) = Setup(body);
        Assert.Empty(sut.Generate(C(1, "Cs", body), topic, graph, 1));
    }
}
