using Unravel.Application.Forge.Llm;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

namespace Unravel.Tests.Forge.Grounded;

/// <summary>
/// Cobre <see cref="DistractorGrammarValidator"/> (PR 34b): comprimento,
/// word count, e shape léxico dos distratores vs resposta correta.
/// Short-circuit pra shapes != FillInTheBlank.
/// </summary>
public class DistractorGrammarValidatorTests
{
    private static readonly DistractorGrammarValidator Sut = new();
    private static readonly ClaimCandidate Claim = new(0, "chunk", "claim", 0.5);

    private static GroundedQuestion FillBlank(int correct, params string[] options) => new(
        Prompt:           "O decorator _____ marca a classe como componente Angular do framework.",
        Options:          options,
        CorrectIndex:     correct,
        Explanation:      "x",
        SourceChunkIndex: 0,
        Shape:            QuestionShape.FillInTheBlank);

    // ── Same-type happy path ──────────────────────────────────────────

    [Fact]
    public void Validate_SymbolPrefixed_AllSame_Passes()
    {
        var q = FillBlank(0, "@Component", "@Directive", "@Injectable", "@NgModule");
        Assert.Null(Sut.Validate(q, Claim));
    }

    [Fact]
    public void Validate_PascalCase_AllSame_Passes()
    {
        var q = FillBlank(0, "AppModule", "AppRouting", "AppService", "AppShared");
        Assert.Null(Sut.Validate(q, Claim));
    }

    [Fact]
    public void Validate_CamelCase_AllSame_Passes()
    {
        var q = FillBlank(0, "useState", "useEffect", "useReducer", "useContext");
        Assert.Null(Sut.Validate(q, Claim));
    }

    [Fact]
    public void Validate_LowerWord_AllSame_Passes()
    {
        var q = FillBlank(0, "selector", "template", "providers", "directive");
        Assert.Null(Sut.Validate(q, Claim));
    }

    // ── Failure modes ────────────────────────────────────────────────

    [Fact]
    public void Validate_DistratorMuchLonger_Fails()
    {
        // Resposta 10 chars; distrator 60 chars → ratio 6.0 > 2.5
        var q = FillBlank(0,
            "@Component",
            "uma classe que implementa a interface IAngularComponentMarker",
            "@Directive",
            "@Injectable");
        var r = Sut.Validate(q, Claim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.DistractorsPoor, r!.Value.Reason);
        Assert.Contains("fora da faixa de tamanho", r.Value.Detail);
    }

    [Fact]
    public void Validate_DistratorMuchShorter_Fails()
    {
        // Resposta 28 chars; distrator 2 chars → ratio 0.07 < 0.40
        var q = FillBlank(0,
            "Through the @Component decorator",
            "ng",
            "Implementando IAngularComponent",
            "Estendendo BaseComponent");
        var r = Sut.Validate(q, Claim);
        Assert.NotNull(r);
        Assert.Contains("fora da faixa de tamanho", r!.Value.Detail);
    }

    [Fact]
    public void Validate_WordCountWildlyDifferent_Fails()
    {
        // Resposta 1 palavra curta; distrator com word count muito diferente.
        // Length também varia, então qualquer um dos dois checks vai pegar —
        // o que importa é que DistractorsPoor é sinalizado.
        var q = FillBlank(0,
            "selector",
            "metadados que descrevem o componente Angular completo",
            "template",
            "providers");
        var r = Sut.Validate(q, Claim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.DistractorsPoor, r!.Value.Reason);
    }

    [Fact]
    public void Validate_MixedShapes_Fails()
    {
        // Resposta @Symbol; só 1 distrator match (33% < 50%)
        var q = FillBlank(0,
            "@Component",      // SymbolPrefixed
            "@Directive",      // SymbolPrefixed  ← match
            "AppModule",       // PascalCase
            "useState");       // CamelCase
        var r = Sut.Validate(q, Claim);
        Assert.NotNull(r);
        Assert.Contains("compartilham forma léxica", r!.Value.Detail);
    }

    // ── Short-circuit ────────────────────────────────────────────────

    [Fact]
    public void Validate_NotFillBlankShape_ShortCircuits()
    {
        var q = new GroundedQuestion(
            "Qual decorator?",
            new[] { "@Component", "implementação muito longa demais", "x", "ng" },  // bagunçado
            0, "x", 0,
            QuestionShape.MultipleChoice);
        Assert.Null(Sut.Validate(q, Claim));
    }

    // ── ClassifyLexShape interno (string-based pra contornar acessibilidade do enum) ──

    [Theory]
    [InlineData("@Component",         "SymbolPrefixed")]
    [InlineData("#header",            "SymbolPrefixed")]
    [InlineData("`const`",            "Backticked")]
    [InlineData("AppModule",          "PascalCase")]
    [InlineData("useState",           "CamelCase")]
    [InlineData("max_pool_size",      "SnakeCase")]
    [InlineData("app-root",           "KebabCase")]
    [InlineData("selector",           "LowerWord")]
    [InlineData("componente Angular", "Phrase")]
    public void ClassifyLexShape_ReturnsExpected(string input, string expectedName)
    {
        var actual = DistractorGrammarValidator.ClassifyLexShape(input);
        Assert.Equal(expectedName, actual.ToString());
    }
}
