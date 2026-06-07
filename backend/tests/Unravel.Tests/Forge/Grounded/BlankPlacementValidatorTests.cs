using Unravel.Application.Forge.Llm;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

namespace Unravel.Tests.Forge.Grounded;

/// <summary>
/// Cobre <see cref="BlankPlacementValidator"/> (PR 34b). Regras testadas:
/// presença do marcador, ocorrência única, contexto mínimo dos dois lados,
/// e short-circuit pra shapes != FillInTheBlank.
/// </summary>
public class BlankPlacementValidatorTests
{
    private static readonly BlankPlacementValidator Sut = new();
    private static readonly ClaimCandidate Claim = new(0, "chunk", "claim", 0.5);

    private static GroundedQuestion FillBlank(string prompt) => new(
        Prompt:           prompt,
        Options:          new[] { "@Component", "@Directive", "@Injectable", "@NgModule" },
        CorrectIndex:     0,
        Explanation:      "x",
        SourceChunkIndex: 0,
        Shape:            QuestionShape.FillInTheBlank);

    [Fact]
    public void Validate_ValidPlacement_Passes()
    {
        var q = FillBlank("O decorator _____ marca a classe como componente Angular e define metadados.");
        Assert.Null(Sut.Validate(q, Claim));
    }

    [Fact]
    public void Validate_NoBlank_Fails()
    {
        var q = FillBlank("O decorator marca a classe como componente Angular e define metadados.");
        var r = Sut.Validate(q, Claim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.SchemaInvalid, r!.Value.Reason);
        Assert.Contains("sem marcador", r.Value.Detail);
    }

    [Fact]
    public void Validate_MultipleBlanks_Fails()
    {
        var q = FillBlank("O _____ é um tipo de _____ no Angular usado para componentes.");
        var r = Sut.Validate(q, Claim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.SchemaInvalid, r!.Value.Reason);
        Assert.Contains("2 lacunas", r.Value.Detail);
    }

    [Fact]
    public void Validate_BlankAtStart_Fails()
    {
        // 1 palavra de contexto à esquerda (só "A" antes) < min 2
        var q = FillBlank("_____ marca a classe como componente Angular no framework.");
        var r = Sut.Validate(q, Claim);
        Assert.NotNull(r);
        Assert.Contains("esquerda", r!.Value.Detail);
    }

    [Fact]
    public void Validate_BlankAtEnd_Fails()
    {
        // 1 palavra depois ("hoje.") < min 2 (período não conta como palavra)
        var q = FillBlank("O decorator marca a classe como componente Angular hoje _____.");
        var r = Sut.Validate(q, Claim);
        Assert.NotNull(r);
        Assert.Contains("direita", r!.Value.Detail);
    }

    [Fact]
    public void Validate_BlankWithSixUnderscores_TreatedAsOne()
    {
        var q = FillBlank("O decorator ______ marca a classe como componente Angular do framework.");
        Assert.Null(Sut.Validate(q, Claim));
    }

    [Fact]
    public void Validate_NotFillBlankShape_ShortCircuits()
    {
        var q = new GroundedQuestion(
            Prompt:           "Qual o nome do decorator?",
            Options:          new[] { "a", "b", "c", "d" },
            CorrectIndex:     0,
            Explanation:      "x",
            SourceChunkIndex: 0,
            Shape:            QuestionShape.MultipleChoice);
        Assert.Null(Sut.Validate(q, Claim));
    }

    [Fact]
    public void Validate_TrueFalseShape_ShortCircuits()
    {
        var q = new GroundedQuestion(
            "afirmação", new[] { "Verdadeiro", "Falso", "x", "y" }, 0, "x", 0,
            QuestionShape.TrueFalseGrounded);
        Assert.Null(Sut.Validate(q, Claim));
    }
}
