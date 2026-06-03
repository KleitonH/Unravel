using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

namespace Unravel.Tests.Forge.Grounded;

public class AnswerLeakageValidatorTests
{
    private readonly AnswerLeakageValidator _sut = new();
    private readonly ClaimCandidate _dummyClaim = new(0, "chunk", "claim.", 0.5);

    private static GroundedQuestion Q(string prompt, string answer, params string[] distractors)
    {
        var options = new[] { answer }.Concat(distractors).ToArray();
        return new GroundedQuestion(prompt, options, 0, "exp", 0);
    }

    [Fact]
    public void Validate_NoLeak_ReturnsNull()
    {
        var q = Q(
            "Qual é a função principal desse mecanismo de injeção?",
            "Resolver dependências automaticamente",
            "Outra", "Outra2", "Outra3");
        Assert.Null(_sut.Validate(q, _dummyClaim));
    }

    [Fact]
    public void Validate_LiteralAnswerInPrompt_Fails()
    {
        var q = Q(
            "Como funciona o sistema de Resolver dependências automaticamente?",
            "Resolver dependências automaticamente",
            "X", "Y", "Z");
        var r = _sut.Validate(q, _dummyClaim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.AnswerLeakage, r!.Value.Reason);
    }

    [Fact]
    public void Validate_TokenOfAnswerInPrompt_Fails()
    {
        // "automaticamente" tem >6 chars e aparece em ambos
        var q = Q(
            "O que acontece automaticamente quando você usa providers?",
            "Resolução automaticamente do grafo de dependências",
            "x", "y", "z");
        var r = _sut.Validate(q, _dummyClaim);
        Assert.NotNull(r);
        Assert.Equal(GenerationFailureReason.AnswerLeakage, r!.Value.Reason);
    }

    [Fact]
    public void Validate_OnlyStopwordsOverlap_DoesNotLeak()
    {
        // 'componente' está na stopword list — não dá leak
        var q = Q(
            "Como o componente trabalha?",
            "Encapsula UI, estado e lógica como classe TypeScript decorada",
            "x", "y", "z");
        Assert.Null(_sut.Validate(q, _dummyClaim));
    }

    [Fact]
    public void Validate_ShortTokensOverlap_DoesNotLeak()
    {
        // Tokens <6 chars não contam (ex: "tag", "css")
        var q = Q(
            "Qual o uso do tag no html?",
            "Marca o início de um elemento via tag de abertura",
            "a", "b", "c");
        Assert.Null(_sut.Validate(q, _dummyClaim));
    }

    [Fact]
    public void Validate_CaseInsensitive()
    {
        // PR 33e: "encapsulamento" virou stopword (era falso positivo
        // recorrente no eval real). Usar palavra non-stopword pra
        // testar case-insensitive (POLIMORFISMO é jargão técnico
        // que não aparece como tema-de-pergunta).
        var q = Q(
            "Qual é o POLIMORFISMO usado nesse contexto?",
            "polimorfismo paramétrico via generics em TypeScript",
            "x", "y", "z");
        Assert.NotNull(_sut.Validate(q, _dummyClaim));
    }

    [Fact]
    public void Order_IsOne()
    {
        Assert.Equal(1, _sut.Order);
    }
}
