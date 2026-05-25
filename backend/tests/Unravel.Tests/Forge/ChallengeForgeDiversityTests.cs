using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Forge;

namespace Unravel.Tests.Forge;

/// <summary>
/// Cobre o passo de diversificação do PR 17 isolando-o de RAKE/Strategies
/// reais: usa stubs de IChallengeStrategy que devolvem drafts pré-cozidos.
/// Isso permite asserir o algoritmo de seleção sem entrar no mérito de
/// quem gera o quê.
/// </summary>
public class ChallengeForgeDiversityTests
{
    private static Topic SingleTopic() =>
        new(1, contentId: 1, trailId: 1, slug: "t1",
            keywords: Array.Empty<Keyword>(), difficultyScore: 0.4, originalOrder: 0);

    private static KnowledgeGraph SingleTopicGraph() =>
        new(1, new[] { SingleTopic() }, Array.Empty<PrerequisiteEdge>());

    private static Content TheContent() =>
        new() { Id = 1, TrailId = 1, Title = "T", Body = "B", IsActive = true };

    /// <summary>Estratégia de teste que devolve N drafts pré-cozidos com
    /// difficulty controlado — assim sabemos exatamente quem deveria
    /// vencer no ranking.</summary>
    private sealed class StubStrategy : IChallengeStrategy
    {
        private readonly ForgeStrategy _kind;
        private readonly int _count;
        private readonly double _difficulty;

        public StubStrategy(ForgeStrategy kind, int count, double difficulty)
        {
            _kind = kind;
            _count = count;
            _difficulty = difficulty;
        }

        public ForgeStrategy Kind => _kind;

        public IReadOnlyList<GeneratedChallengeDraft> Generate(
            Content content, Topic topic, KnowledgeGraph graph, int maxDrafts)
        {
            // Gera N drafts triviais válidos pro QualityGate.
            // Distratores precisam ter Levenshtein > 1 entre si (gate
            // rejeita "Bar/Baz" e similares).
            var opts = new[] { "Alpha", "Bravo", "Charlie", "Delta" };
            return Enumerable.Range(0, _count).Select(i => new GeneratedChallengeDraft(
                SourceTopicId:       topic.Id,
                SourceContentId:     content.Id,
                Strategy:            _kind,
                Prompt:              $"Pergunta {_kind} número {i} com texto longo o suficiente.",
                Options:             opts,
                CorrectIndex:        i % 4,
                Explanation:         "exp",
                EstimatedDifficulty: _difficulty)).ToList();
        }
    }

    private static ChallengeForge ForgeWith(params IChallengeStrategy[] strats) =>
        new(strats, graphCache: null!);

    // ── Casos ────────────────────────────────────────────────────────

    [Fact]
    public void Build_OneStrategyAvailable_NoDiversificationApplied()
    {
        var forge = ForgeWith(new StubStrategy(ForgeStrategy.Cloze, count: 6, difficulty: 0.4));
        var pool = forge.Build(TheContent(), SingleTopicGraph(), targetCount: 5);
        // Debug: lista os prompts pra entender o que sobrou
        var prompts = string.Join(" | ", pool.Select(d => d.Prompt));
        Assert.True(pool.Count == 5, $"expected 5 drafts, got {pool.Count}. Prompts: {prompts}");
    }

    [Fact]
    public void Build_FivePoolWithThreeStrategiesAvailable_GuaranteesThreeDistinct()
    {
        // Cloze tem fitness MAIOR (difficulty mais próxima do target);
        // sem diversificação, satura 5/5. Com PR 17, deve ceder 2 slots
        // pra Definition e TrueFalse.
        var forge = ForgeWith(
            new StubStrategy(ForgeStrategy.Cloze,      count: 6, difficulty: 0.45),
            new StubStrategy(ForgeStrategy.Definition, count: 3, difficulty: 0.20),
            new StubStrategy(ForgeStrategy.TrueFalse,  count: 2, difficulty: 0.20));

        var pool = forge.Build(TheContent(), SingleTopicGraph(),
                               targetCount: 5, targetUserMastery: 0.3);

        Assert.Equal(5, pool.Count);
        var distinct = pool.Select(d => d.Strategy).Distinct().Count();
        Assert.True(distinct >= 3,
            $"esperava >= 3 estratégias distintas; obtive {distinct} ({string.Join(",", pool.Select(d => d.Strategy))})");
    }

    [Fact]
    public void Build_FewerThanMinDistinctStrategiesExist_StaysWithinAvailable()
    {
        // Só 2 estratégias disponíveis — não pode forçar 3.
        var forge = ForgeWith(
            new StubStrategy(ForgeStrategy.Cloze,      count: 4, difficulty: 0.4),
            new StubStrategy(ForgeStrategy.Definition, count: 4, difficulty: 0.4));

        var pool = forge.Build(TheContent(), SingleTopicGraph(), targetCount: 5);
        Assert.Equal(5, pool.Count);
        var distinct = pool.Select(d => d.Strategy).Distinct().Count();
        Assert.Equal(2, distinct);   // saturou as 2 disponíveis
    }

    [Fact]
    public void Build_IsDeterministic_SameInputSameOrder()
    {
        var strats = new IChallengeStrategy[]
        {
            new StubStrategy(ForgeStrategy.Cloze,      count: 4, difficulty: 0.45),
            new StubStrategy(ForgeStrategy.Definition, count: 3, difficulty: 0.25),
            new StubStrategy(ForgeStrategy.TrueFalse,  count: 2, difficulty: 0.55),
        };
        var forge = ForgeWith(strats);

        var a = forge.Build(TheContent(), SingleTopicGraph(), 5);
        var b = forge.Build(TheContent(), SingleTopicGraph(), 5);

        Assert.Equal(a.Select(d => d.Prompt), b.Select(d => d.Prompt));
    }

    [Fact]
    public void Build_TargetCountSmallerThanMinDistinct_LimitsToTargetCount()
    {
        var forge = ForgeWith(
            new StubStrategy(ForgeStrategy.Cloze,      count: 4, difficulty: 0.45),
            new StubStrategy(ForgeStrategy.Definition, count: 3, difficulty: 0.25),
            new StubStrategy(ForgeStrategy.TrueFalse,  count: 2, difficulty: 0.55));

        var pool = forge.Build(TheContent(), SingleTopicGraph(), targetCount: 2);
        Assert.Equal(2, pool.Count);
    }
}
