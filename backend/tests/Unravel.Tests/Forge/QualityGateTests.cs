using Unravel.Application.Forge;
using Unravel.Domain.Forge;

namespace Unravel.Tests.Forge;

public class QualityGateTests
{
    private static GeneratedChallengeDraft Draft(
        string prompt   = "Qual o melhor framework para construir SPAs em TypeScript?",
        string[]? opts  = null,
        int correctIdx  = 0)
        => new(
            SourceTopicId: 1, SourceContentId: 1, Strategy: ForgeStrategy.Cloze,
            Prompt: prompt,
            Options: opts ?? new[] { "Angular", "Bash", "PostgreSQL", "TCP" },
            CorrectIndex: correctIdx,
            Explanation: null,
            EstimatedDifficulty: 0.5);

    [Fact]
    public void Approve_ValidDraft_PassesWithoutReason()
    {
        Assert.True(QualityGate.Approve(Draft(), out var reason));
        Assert.Null(reason);
    }

    [Fact]
    public void Approve_PromptTooShort_Rejects()
    {
        Assert.False(QualityGate.Approve(Draft(prompt: "Curto?"), out var reason));
        Assert.Equal("prompt_too_short", reason);
    }

    [Fact]
    public void Approve_TooFewOptions_Rejects()
    {
        Assert.False(QualityGate.Approve(Draft(opts: new[] { "A", "B" }), out var reason));
        Assert.Equal("options_out_of_range", reason);
    }

    [Fact]
    public void Approve_DuplicateOptions_RejectsEvenWhenCaseOrDiacriticDiffers()
    {
        var d = Draft(opts: new[] { "Angular", "angular", "React", "Vue" });
        Assert.False(QualityGate.Approve(d, out var reason));
        Assert.Equal("duplicate_options", reason);

        var d2 = Draft(opts: new[] { "Programação", "Programacao", "React", "Vue" });
        Assert.False(QualityGate.Approve(d2, out reason));
        Assert.Equal("duplicate_options", reason);
    }

    [Fact]
    public void Approve_OptionsTooSimilarToCorrect_Rejects()
    {
        // "Angular" / "Angula" = 1 edit (delete 'r') → rejeitado pelo limiar ≤ 1.
        var d = Draft(opts: new[] { "Angular", "Angula", "React", "Vue" });
        Assert.False(QualityGate.Approve(d, out var reason));
        Assert.Equal("options_too_similar_to_correct", reason);
    }

    [Fact]
    public void Approve_CorrectIndexOutOfRange_Rejects()
    {
        Assert.False(QualityGate.Approve(Draft(correctIdx: 99), out var reason));
        Assert.Equal("correct_index_out_of_range", reason);
    }

    [Fact]
    public void Approve_EmptyOption_Rejects()
    {
        Assert.False(QualityGate.Approve(Draft(opts: new[] { "Angular", "  ", "React", "Vue" }), out var reason));
        Assert.Equal("empty_option", reason);
    }
}
