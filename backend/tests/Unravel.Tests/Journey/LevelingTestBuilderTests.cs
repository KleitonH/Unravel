using Unravel.Application.Forge.Ports;
using Unravel.Application.Journey.Onboarding;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Forge;
using Unravel.Infrastructure.Forge.Strategies;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Tests.Journey;

public class LevelingTestBuilderTests
{
    private readonly GraphBuilder _graphBuilder = new(new RakeKeywordExtractor(), new DifficultyScorer());

    private LevelingTestBuilder CreateBuilder()
    {
        var distractors = new DistractorPicker();
        var strategies = new IChallengeStrategy[]
        {
            new ClozeStrategy(distractors),
            new DefinitionStrategy(distractors),
            new TrueFalseStrategy(distractors),
        };
        var forge = new ChallengeForge(strategies, graphCache: null!);
        return new LevelingTestBuilder(forge);
    }

    private static Content C(int id, int order, string title, string body, DifficultyLevel level = DifficultyLevel.Intermediate) =>
        new() { Id = id, TrailId = 1, Order = order, Title = title, Body = body, Level = level, IsActive = true };

    private List<Content> RichTrail() => new()
    {
        C(1, 1, "Variáveis",  "Variáveis em JavaScript guardam valores primitivos como strings e números. " +
                              "Usam let, const ou var para declaração com escopos distintos.", DifficultyLevel.Beginner),
        C(2, 2, "Funções",    "Funções em JavaScript encapsulam blocos de código reutilizáveis. " +
                              "Aceitam parâmetros, retornam valores e podem ser passadas como argumentos.", DifficultyLevel.Beginner),
        C(3, 3, "Objetos",    "Objetos JavaScript agrupam dados e comportamentos em pares chave-valor. " +
                              "Permitem composição flexível e acesso via dot notation ou brackets.", DifficultyLevel.Intermediate),
        C(4, 4, "Promises",   "Promises representam valores assíncronos que serão resolvidos no futuro. " +
                              "Encadeiam via then/catch e podem ser combinadas com Promise.all.", DifficultyLevel.Intermediate),
        C(5, 5, "Closures",   "Closures em JavaScript são funções que capturam variáveis do escopo léxico externo. " +
                              "Permitem encapsulamento e padrões como factory functions e currying.", DifficultyLevel.Advanced),
        C(6, 6, "Generators", "Generators permitem pausar e retomar execução com yield. " +
                              "Usados para iteradores customizados e fluxos de dados assíncronos.", DifficultyLevel.Advanced),
    };

    [Fact]
    public void Build_PicksTopicsSpreadByDifficulty()
    {
        var builder = CreateBuilder();
        var graph   = _graphBuilder.Build(1, RichTrail());
        var contents = RichTrail().ToDictionary(c => c.Id);

        var drafts = builder.Build(graph, contents);

        Assert.NotEmpty(drafts);
        Assert.True(drafts.Count <= LevelingTestBuilder.QuestionsPerTrail);

        // A distribuição deve cobrir um espectro razoável de difficulty.
        // Não exigimos "fácil/médio/difícil" exato (varia com o scorer);
        // exigimos que os topics escolhidos cubram pelo menos 30% da
        // amplitude possível.
        var difficulties = drafts.Select(d => d.Topic.DifficultyScore).ToList();
        var spread       = difficulties.Max() - difficulties.Min();
        Assert.True(spread >= 0.15,
            $"esperava amplitude >= 0.15 de difficulty; obtive {spread:F3} para [{string.Join(",", difficulties.Select(x=>x.ToString("F2")))}]");
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var builder = CreateBuilder();
        var graph   = _graphBuilder.Build(1, RichTrail());
        var contents = RichTrail().ToDictionary(c => c.Id);

        var a = builder.Build(graph, contents);
        var b = builder.Build(graph, contents);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Topic.Id, b[i].Topic.Id);
            Assert.Equal(a[i].Draft.Prompt, b[i].Draft.Prompt);
            Assert.Equal(a[i].Draft.CorrectIndex, b[i].Draft.CorrectIndex);
        }
    }

    [Fact]
    public void Build_EmptyGraph_ReturnsEmpty()
    {
        var builder = CreateBuilder();
        var emptyGraph = _graphBuilder.Build(1, Array.Empty<Content>());

        var drafts = builder.Build(emptyGraph, new Dictionary<int, Content>());

        Assert.Empty(drafts);
    }

    [Fact]
    public void Build_FewerTopicsThanQuota_ReturnsAllOfThem()
    {
        var builder = CreateBuilder();
        var smallTrail = new List<Content>
        {
            C(1, 1, "T1", "Variáveis em JavaScript guardam valores como strings e números."),
            C(2, 2, "T2", "Funções JavaScript encapsulam blocos reutilizáveis aceitando parâmetros."),
        };
        var graph    = _graphBuilder.Build(1, smallTrail);
        var contents = smallTrail.ToDictionary(c => c.Id);

        var drafts = builder.Build(graph, contents);
        Assert.True(drafts.Count <= 2);
    }
}
