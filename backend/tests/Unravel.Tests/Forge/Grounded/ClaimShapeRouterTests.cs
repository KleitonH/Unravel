using Unravel.Application.Forge.Llm;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded;

namespace Unravel.Tests.Forge.Grounded;

/// <summary>
/// Cobre <see cref="ClaimShapeRouter"/> (PR 34a). Heurística:
/// FillInTheBlank requer (a) 7-28 palavras E (b) termo técnico
/// (PascalCase, camelCase, snake_case ou entre crases); senão MCQ.
/// </summary>
public class ClaimShapeRouterTests
{
    private static readonly ClaimShapeRouter Sut = new();

    private static ClaimCandidate Claim(string text) =>
        new(ChunkIndex: 0, ChunkText: text, ClaimText: text, Score: 0.5);

    // ─── FillInTheBlank: caminho feliz ────────────────────────────────

    [Theory]
    [InlineData("O decorator @Component marca a classe como componente Angular do framework.")]
    [InlineData("A função useState do React permite gerenciar estado em componentes funcionais.")]
    [InlineData("O atributo max_pool_size controla quantas conexões podem existir simultaneamente.")]
    [InlineData("A diretiva ngFor itera sobre uma coleção e renderiza um template para cada item.")]
    [InlineData("O método toString retorna uma representação textual do objeto chamador.")]
    public void Route_GoodFillBlankCandidate_ReturnsFillInTheBlank(string text)
    {
        var d = Sut.Route(Claim(text));
        Assert.Equal(QuestionShape.FillInTheBlank, d.Shape);
        Assert.Equal("good_shape_match", d.Reason);
    }

    [Fact]
    public void Route_ClaimWithBackticks_ReturnsFillInTheBlank()
    {
        var text = "O comando `npm install` baixa as dependências listadas no package json do projeto.";
        var d = Sut.Route(Claim(text));
        Assert.Equal(QuestionShape.FillInTheBlank, d.Shape);
    }

    // ─── MultipleChoice: muito curto ──────────────────────────────────

    [Theory]
    [InlineData("É rápido.")]                                      // 2 palavras
    [InlineData("O Angular é um framework moderno.")]              // 6 palavras
    public void Route_TooShortClaim_ReturnsMultipleChoice(string text)
    {
        var d = Sut.Route(Claim(text));
        Assert.Equal(QuestionShape.MultipleChoice, d.Shape);
        Assert.Equal("claim_too_short", d.Reason);
    }

    // ─── MultipleChoice: muito longo ──────────────────────────────────

    [Fact]
    public void Route_TooLongClaim_ReturnsMultipleChoice()
    {
        // 30 palavras (> 28)
        var text = string.Join(' ', Enumerable.Repeat("palavra", 30)) + " com TermoTecnico no meio.";
        var d = Sut.Route(Claim(text));
        Assert.Equal(QuestionShape.MultipleChoice, d.Shape);
        Assert.Equal("claim_too_long", d.Reason);
    }

    // ─── MultipleChoice: sem termo técnico ────────────────────────────

    [Theory]
    [InlineData("O texto possui muitas palavras importantes para entender o assunto completo do material.")]
    [InlineData("Esta frase tem tamanho médio mas nenhuma referência técnica específica do tema apresentado aqui.")]
    public void Route_NoTechnicalTerm_ReturnsMultipleChoice(string text)
    {
        var d = Sut.Route(Claim(text));
        Assert.Equal(QuestionShape.MultipleChoice, d.Shape);
        Assert.Equal("no_technical_term", d.Reason);
    }

    // ─── Determinismo ─────────────────────────────────────────────────

    [Fact]
    public void Route_IsDeterministic()
    {
        var c = Claim("O decorator @Component marca a classe como componente Angular.");
        var a = Sut.Route(c);
        var b = Sut.Route(c);
        Assert.Equal(a.Shape,  b.Shape);
        Assert.Equal(a.Reason, b.Reason);
    }

    [Fact]
    public void Route_ThrowsOnNullClaim()
    {
        Assert.Throws<ArgumentNullException>(() => Sut.Route(null!));
    }

    [Fact]
    public void Route_EmptyClaimText_FallsBackToMcq_DueToWordCount()
    {
        var d = Sut.Route(Claim(string.Empty));
        Assert.Equal(QuestionShape.MultipleChoice, d.Shape);
        Assert.Equal("claim_too_short", d.Reason);
    }
}
