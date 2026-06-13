using Unravel.Application.Forge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded;

namespace Unravel.Tests.Forge.Grounded;

/// <summary>
/// PR 34g — cobre o gerador de guidance de reflexion. Garante que cada
/// FailureReason produz instrução específica e acionável (não genérica),
/// que o detalhe técnico é incluído, e que o número da tentativa aparece.
/// </summary>
public class RetryGuidanceTests
{
    [Theory]
    [InlineData(GenerationFailureReason.AnswerLeakage,    "VAZOU")]
    [InlineData(GenerationFailureReason.AnswerNotGrounded, "base clara")]
    [InlineData(GenerationFailureReason.DistractorsPoor,  "distratores")]
    [InlineData(GenerationFailureReason.SchemaInvalid,    "estrutura")]
    [InlineData(GenerationFailureReason.JsonParseError,   "JSON")]
    public void Build_IncludesReasonSpecificGuidance(GenerationFailureReason reason, string marker)
    {
        var fb = new RetryFeedback(reason, "detalhe X", AttemptNumber: 1);
        var g  = RetryGuidance.Build(fb);
        Assert.Contains(marker, g);
        Assert.Contains("AUTOCORREÇÃO", g);
    }

    [Fact]
    public void Build_IncludesDetail()
    {
        var fb = new RetryFeedback(GenerationFailureReason.DistractorsPoor,
            "Apenas 0/3 distratores compartilham forma", AttemptNumber: 1);
        var g = RetryGuidance.Build(fb);
        Assert.Contains("Apenas 0/3 distratores compartilham forma", g);
    }

    [Fact]
    public void Build_NullDetail_OmitsDetailLine()
    {
        var fb = new RetryFeedback(GenerationFailureReason.AnswerLeakage, null, AttemptNumber: 1);
        var g  = RetryGuidance.Build(fb);
        Assert.DoesNotContain("Detalhe técnico", g);
    }

    [Fact]
    public void Build_ShowsNextAttemptNumber()
    {
        // AttemptNumber=1 (tentativa anterior) → mostra "tentativa 2"
        var fb = new RetryFeedback(GenerationFailureReason.SchemaInvalid, "x", AttemptNumber: 1);
        var g  = RetryGuidance.Build(fb);
        Assert.Contains("tentativa 2", g);
    }

    [Fact]
    public void Build_UnknownReason_FallsBackToGeneric()
    {
        var fb = new RetryFeedback(GenerationFailureReason.None, null, AttemptNumber: 1);
        var g  = RetryGuidance.Build(fb);
        Assert.Contains("validação de qualidade", g);
    }
}
