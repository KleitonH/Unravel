using Unravel.Application.Forge;

namespace Unravel.Tests.Forge;

/// <summary>
/// PR 60-f — cobre a montagem/validação da pergunta autoral do moderador.
/// A factory é pura (sem DB), então testa validação de campos, distinção
/// das opções e o posicionamento determinístico da resposta correta.
/// </summary>
public class AuthoredQuestionTests
{
    private static readonly List<string> ValidDistractors = new() { "Beta", "Gamma", "Delta" };

    [Fact]
    public void Build_Valid_ProducesFourDistinctOptionsWithCorrectAtSeedIndex()
    {
        var r = AuthoredQuestion.Build("Qual o primeiro?", "Alpha", ValidDistractors, positionSeed: 2);

        Assert.True(r.Ok);
        Assert.Null(r.Error);
        Assert.Equal(4, r.Options.Length);
        Assert.Equal(4, r.Options.Distinct().Count());
        // seed 2 → rotação à direita por 2 → correta no índice 2
        Assert.Equal(2, r.CorrectIndex);
        Assert.Equal("Alpha", r.Options[r.CorrectIndex]);
        Assert.Contains("Beta", r.Options);
        Assert.Contains("Gamma", r.Options);
        Assert.Contains("Delta", r.Options);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 0)]   // wrap
    [InlineData(7, 3)]
    [InlineData(-1, 3)]  // seed negativo normaliza
    public void Build_CorrectIndex_FollowsSeedModulo(int seed, int expectedIndex)
    {
        var r = AuthoredQuestion.Build("p", "Alpha", ValidDistractors, seed);
        Assert.True(r.Ok);
        Assert.Equal(expectedIndex, r.CorrectIndex);
        Assert.Equal("Alpha", r.Options[r.CorrectIndex]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_MissingPrompt_Fails(string? prompt)
    {
        var r = AuthoredQuestion.Build(prompt, "Alpha", ValidDistractors, 0);
        Assert.False(r.Ok);
        Assert.Contains("Enunciado", r.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Build_MissingCorrect_Fails(string? correct)
    {
        var r = AuthoredQuestion.Build("p", correct, ValidDistractors, 0);
        Assert.False(r.Ok);
        Assert.Contains("Resposta correta", r.Error);
    }

    [Fact]
    public void Build_WrongDistractorCount_Fails()
    {
        var r = AuthoredQuestion.Build("p", "Alpha", new List<string> { "Beta", "Gamma" }, 0);
        Assert.False(r.Ok);
        Assert.Contains("3 distratores", r.Error);
    }

    [Fact]
    public void Build_EmptyDistractor_Fails()
    {
        var r = AuthoredQuestion.Build("p", "Alpha", new List<string> { "Beta", "", "Delta" }, 0);
        Assert.False(r.Ok);
        Assert.Contains("3 distratores", r.Error);
    }

    [Fact]
    public void Build_DuplicateAcrossCorrectAndDistractor_Fails()
    {
        // "alpha" duplica "Alpha" case-insensitive
        var r = AuthoredQuestion.Build("p", "Alpha", new List<string> { "alpha", "Gamma", "Delta" }, 0);
        Assert.False(r.Ok);
        Assert.Contains("distintas", r.Error);
    }

    [Fact]
    public void Build_TrimsWhitespace()
    {
        var r = AuthoredQuestion.Build("p", "  Alpha  ", new List<string> { " Beta ", "Gamma", "Delta" }, 0);
        Assert.True(r.Ok);
        Assert.Contains("Alpha", r.Options);
        Assert.Contains("Beta", r.Options);
        Assert.DoesNotContain("  Alpha  ", r.Options);
    }
}
