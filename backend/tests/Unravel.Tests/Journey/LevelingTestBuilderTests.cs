using System.Text.Json;
using Unravel.Application.Journey.Onboarding;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;

namespace Unravel.Tests.Journey;

public class LevelingTestBuilderTests
{
    private readonly LevelingTestBuilder _builder = new();

    private static Content C(int id, string title) =>
        new() { Id = id, TrailId = 1, Order = id, Title = title, Body = "corpo", Level = DifficultyLevel.Intermediate, IsActive = true };

    private static GeneratedChallenge GC(int id, int contentId, double difficulty, int correctIndex = 0) =>
        new()
        {
            Id = id, ContentId = contentId, TopicId = contentId, TrailId = 1,
            Strategy = ForgeStrategy.LlmGrounded, Prompt = $"Pergunta {id}?",
            EstimatedDifficulty = difficulty, IsActive = true,
            BodyJson = JsonSerializer.Serialize(new
            {
                options = new[] { "A", "B", "C", "D" }, correctIndex, explanation = (string?)null,
            }),
        };

    // Trilha com 8 conteúdos, 1 pergunta cada, dificuldades espalhadas.
    private static (List<GeneratedChallenge>, Dictionary<int, Content>) RichTrail()
    {
        var challenges = new List<GeneratedChallenge>();
        var contents   = new Dictionary<int, Content>();
        for (var i = 1; i <= 8; i++)
        {
            challenges.Add(GC(id: i, contentId: i, difficulty: i / 8.0));
            contents[i] = C(i, $"Conteúdo {i}");
        }
        return (challenges, contents);
    }

    [Fact]
    public void Build_CapsAtQuestionsPerTrail_AndSpreadsByDifficulty()
    {
        var (challenges, contents) = RichTrail();

        var drafts = _builder.Build(challenges, contents);

        Assert.Equal(LevelingTestBuilder.QuestionsPerTrail, drafts.Count); // 6 de 8
        var difficulties = drafts.Select(d => d.Draft.EstimatedDifficulty).ToList();
        Assert.True(difficulties.Max() - difficulties.Min() >= 0.3,
            $"esperava amplitude de dificuldade; obtive [{string.Join(",", difficulties.Select(x => x.ToString("F2")))}]");
        // resultado ordenado do fácil ao difícil
        Assert.True(difficulties.SequenceEqual(difficulties.OrderBy(x => x)));
    }

    [Fact]
    public void Build_SingleContentTrail_StillYieldsFullTest()
    {
        // Conteúdo único com 10 perguntas (como PHP Avançado): deve render 6.
        var challenges = Enumerable.Range(1, 10)
            .Select(i => GC(id: i, contentId: 99, difficulty: i / 10.0))
            .ToList();
        var contents = new Dictionary<int, Content> { [99] = C(99, "Artigão") };

        var drafts = _builder.Build(challenges, contents);

        Assert.Equal(LevelingTestBuilder.QuestionsPerTrail, drafts.Count);
        Assert.All(drafts, d => Assert.Equal(99, d.Content.Id));
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var (challenges, contents) = RichTrail();

        var a = _builder.Build(challenges, contents);
        var b = _builder.Build(challenges, contents);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].ChallengeId, b[i].ChallengeId);
            Assert.Equal(a[i].Draft.Prompt, b[i].Draft.Prompt);
        }
    }

    [Fact]
    public void Build_EmptyInput_ReturnsEmpty()
        => Assert.Empty(_builder.Build(Array.Empty<GeneratedChallenge>(), new Dictionary<int, Content>()));

    [Fact]
    public void Build_FewerThanQuota_ReturnsAll()
    {
        var challenges = new List<GeneratedChallenge> { GC(1, 1, 0.2), GC(2, 2, 0.6) };
        var contents   = new Dictionary<int, Content> { [1] = C(1, "T1"), [2] = C(2, "T2") };

        var drafts = _builder.Build(challenges, contents);
        Assert.Equal(2, drafts.Count);
    }

    [Fact]
    public void Build_PrefersContentDiversity()
    {
        // 2 conteúdos: A com 5 perguntas, B com 5. Espera 3 de cada (round-robin).
        var challenges = new List<GeneratedChallenge>();
        for (var i = 1; i <= 5; i++)  challenges.Add(GC(i,      contentId: 1, difficulty: i / 10.0));
        for (var i = 1; i <= 5; i++)  challenges.Add(GC(100 + i, contentId: 2, difficulty: i / 10.0));
        var contents = new Dictionary<int, Content> { [1] = C(1, "A"), [2] = C(2, "B") };

        var drafts = _builder.Build(challenges, contents);

        Assert.Equal(6, drafts.Count);
        Assert.Equal(3, drafts.Count(d => d.Content.Id == 1));
        Assert.Equal(3, drafts.Count(d => d.Content.Id == 2));
    }
}
