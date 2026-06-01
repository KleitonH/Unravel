using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

namespace Unravel.Tests.Forge.Grounded;

public class SchemaValidatorTests
{
    private readonly SchemaValidator _sut = new();
    private readonly ClaimCandidate  _dummyClaim = new(0, "chunk text", "claim.", 0.5);

    private static GroundedQuestion ValidQuestion(string prompt = "Qual é a função correta do decorator Angular?",
        string[]? opts = null, int correct = 0, string? explanation = "Justificativa.") =>
        new(prompt, opts ?? new[] { "A", "B", "C", "D" }, correct, explanation, 0);

    [Fact]
    public void Validate_HappyPath_ReturnsNull()
    {
        var q = ValidQuestion();
        Assert.Null(_sut.Validate(q, _dummyClaim));
    }

    [Fact]
    public void Validate_EmptyPrompt_Fails()
    {
        var q = ValidQuestion(prompt: "");
        var r = _sut.Validate(q, _dummyClaim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.SchemaInvalid, r!.Value.Reason);
    }

    [Fact]
    public void Validate_TooShortPrompt_Fails()
    {
        var q = ValidQuestion(prompt: "Curto");
        Assert.NotNull(_sut.Validate(q, _dummyClaim));
    }

    [Fact]
    public void Validate_WrongOptionCount_Fails()
    {
        var q = ValidQuestion(opts: new[] { "A", "B", "C" });
        var r = _sut.Validate(q, _dummyClaim);
        Assert.Equal(GenerationFailureReason.SchemaInvalid, r!.Value.Reason);
        Assert.Contains("3", r.Value.Detail);
    }

    [Fact]
    public void Validate_DuplicateOptions_Fails()
    {
        var q = ValidQuestion(opts: new[] { "Mesmo", "Mesmo", "Outro", "Outro2" });
        var r = _sut.Validate(q, _dummyClaim);
        Assert.Equal(GenerationFailureReason.SchemaInvalid, r!.Value.Reason);
        Assert.Contains("duplicat", r.Value.Detail);
    }

    [Fact]
    public void Validate_DuplicateOptionsCaseInsensitive_Fails()
    {
        var q = ValidQuestion(opts: new[] { "Standalone", "standalone", "Outro", "Outro2" });
        Assert.NotNull(_sut.Validate(q, _dummyClaim));
    }

    [Fact]
    public void Validate_EmptyOption_Fails()
    {
        var q = ValidQuestion(opts: new[] { "A", "", "C", "D" });
        Assert.NotNull(_sut.Validate(q, _dummyClaim));
    }

    [Fact]
    public void Validate_CorrectIndexOutOfRange_Fails()
    {
        Assert.NotNull(_sut.Validate(ValidQuestion(correct: -1), _dummyClaim));
        Assert.NotNull(_sut.Validate(ValidQuestion(correct: 4), _dummyClaim));
    }

    [Fact]
    public void Validate_NoExplanation_Fails()
    {
        Assert.NotNull(_sut.Validate(ValidQuestion(explanation: null), _dummyClaim));
        Assert.NotNull(_sut.Validate(ValidQuestion(explanation: ""), _dummyClaim));
    }

    [Fact]
    public void Order_IsZero()
    {
        Assert.Equal(0, _sut.Order);
    }
}
