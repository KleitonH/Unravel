using Microsoft.Extensions.Logging.Abstractions;
using Unravel.Application.Forge.Llm;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded;
using Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

namespace Unravel.Tests.Forge.Grounded;

/// <summary>
/// Cobre o orquestrador <see cref="LlmGroundedQuestionGenerator"/>:
/// pipeline (prompt → LLM → parse → validators), tratamento de falha
/// em cada estágio, e que validators rodam em ordem (short-circuit).
///
/// <para>LLM e validadores são stubs/fakes — sem rede, sem ONNX.</para>
/// </summary>
public class LlmGroundedQuestionGeneratorTests
{
    private static readonly ClaimCandidate Claim = new(
        ChunkIndex: 1,
        ChunkText:  "O decorator @Component marca a classe como componente Angular.",
        ClaimText:  "O decorator @Component marca a classe como componente Angular.",
        Score:      0.9);

    private sealed class StubLlm : ILlmInference
    {
        public string? NextResponse { get; set; }
        public Exception? Throws { get; set; }
        public int CallCount { get; private set; }
        public string? LastPrompt { get; private set; }

        public Task<string?> CompleteAsync(string prompt, CancellationToken ct = default)
        {
            CallCount++;
            LastPrompt = prompt;
            if (Throws is not null) throw Throws;
            return Task.FromResult(NextResponse);
        }
    }

    private sealed class AlwaysFailValidator(int order, GenerationFailureReason r) : IQuestionValidator
    {
        public int CallCount { get; private set; }
        public int Order { get; } = order;
        public (GenerationFailureReason Reason, string Detail)? Validate(GroundedQuestion q, ClaimCandidate c)
        {
            CallCount++;
            return (r, "stub fail");
        }
    }

    private sealed class AlwaysPassValidator(int order) : IQuestionValidator
    {
        public int CallCount { get; private set; }
        public int Order { get; } = order;
        public (GenerationFailureReason Reason, string Detail)? Validate(GroundedQuestion q, ClaimCandidate c)
        {
            CallCount++;
            return null;
        }
    }

    /// <summary>Stub do router pra forçar shape específico em cada teste
    /// (default <see cref="QuestionShape.MultipleChoice"/> mantém retrocompat
    /// com os testes anteriores ao PR 34a).</summary>
    private sealed class FixedShapeRouter(QuestionShape shape = QuestionShape.MultipleChoice) : IClaimShapeRouter
    {
        public ShapeDecision Route(ClaimCandidate claim) => new(shape, "fixed_stub");
    }

    private static LlmGroundedQuestionGenerator Build(StubLlm llm, params IQuestionValidator[] validators) =>
        new(llm, new FixedShapeRouter(), validators, NullLogger<LlmGroundedQuestionGenerator>.Instance);

    private static LlmGroundedQuestionGenerator BuildWithShape(StubLlm llm, QuestionShape shape, params IQuestionValidator[] validators) =>
        new(llm, new FixedShapeRouter(shape), validators, NullLogger<LlmGroundedQuestionGenerator>.Instance);

    private const string ValidJsonResponse = """
        {
          "prompt": "Qual decorator marca uma classe como componente Angular?",
          "options": ["@Component", "@Directive", "@Pipe", "@NgModule"],
          "correctIndex": 0,
          "explanation": "O decorator @Component identifica componentes."
        }
        """;

    [Fact]
    public async Task Generate_HappyPath_ReturnsValidatedQuestion()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var sut = Build(llm, new AlwaysPassValidator(0));

        var result = await sut.GenerateAsync(Claim, "Componentes Angular");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Question);
        Assert.Equal("@Component", result.Question!.Options[0]);
        Assert.Equal(0, result.Question.CorrectIndex);
        Assert.Equal(1, result.Question.SourceChunkIndex); // preservado do claim
        Assert.Equal(GenerationFailureReason.None, result.FailureReason);
    }

    [Fact]
    public async Task Generate_LlmReturnsNull_FailsWithLlmEmpty()
    {
        var llm = new StubLlm { NextResponse = null };
        var sut = Build(llm);

        var result = await sut.GenerateAsync(Claim, "Title");

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureReason.LlmEmpty, result.FailureReason);
    }

    [Fact]
    public async Task Generate_LlmReturnsEmptyString_FailsWithLlmEmpty()
    {
        var llm = new StubLlm { NextResponse = "   " };
        var sut = Build(llm);

        var result = await sut.GenerateAsync(Claim, "Title");
        Assert.Equal(GenerationFailureReason.LlmEmpty, result.FailureReason);
    }

    [Fact]
    public async Task Generate_LlmReturnsInvalidJson_FailsWithJsonParseError()
    {
        var llm = new StubLlm { NextResponse = "isso não é json {{ broken" };
        var sut = Build(llm);

        var result = await sut.GenerateAsync(Claim, "Title");
        Assert.Equal(GenerationFailureReason.JsonParseError, result.FailureReason);
    }

    [Fact]
    public async Task Generate_LlmThrows_FailsGracefully()
    {
        var llm = new StubLlm { Throws = new InvalidOperationException("daemon down") };
        var sut = Build(llm);

        var result = await sut.GenerateAsync(Claim, "Title");
        Assert.Equal(GenerationFailureReason.LlmEmpty, result.FailureReason);
        Assert.Contains("daemon down", result.FailureDetail);
    }

    [Fact]
    public async Task Generate_ValidatorFails_PropagatesReason()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var validator = new AlwaysFailValidator(0, GenerationFailureReason.AnswerLeakage);
        var sut = Build(llm, validator);

        var result = await sut.GenerateAsync(Claim, "Title");

        Assert.False(result.IsSuccess);
        Assert.Equal(GenerationFailureReason.AnswerLeakage, result.FailureReason);
        Assert.Equal(1, validator.CallCount);
    }

    [Fact]
    public async Task Generate_ValidatorsRunInOrder_ShortCircuitOnFirstFail()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        // Validador "order=0" passa, "order=1" falha → "order=2" não roda
        var v0 = new AlwaysPassValidator(0);
        var v1 = new AlwaysFailValidator(1, GenerationFailureReason.AnswerLeakage);
        var v2 = new AlwaysPassValidator(2);
        // Passa fora de ordem pra confirmar que ele ordena internamente
        var sut = Build(llm, v2, v0, v1);

        var result = await sut.GenerateAsync(Claim, "Title");

        Assert.Equal(GenerationFailureReason.AnswerLeakage, result.FailureReason);
        Assert.Equal(1, v0.CallCount);
        Assert.Equal(1, v1.CallCount);
        Assert.Equal(0, v2.CallCount); // short-circuit
    }

    [Fact]
    public async Task Generate_AllValidatorsPass_ReturnsSuccess()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var validators = new IQuestionValidator[]
        {
            new AlwaysPassValidator(0),
            new AlwaysPassValidator(1),
            new AlwaysPassValidator(2),
        };
        var sut = Build(llm, validators);

        var result = await sut.GenerateAsync(Claim, "Title");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Generate_PromptContainsClaimAndChunk()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var sut = Build(llm, new AlwaysPassValidator(0));

        await sut.GenerateAsync(Claim, "Componentes Angular");

        Assert.Contains("Componentes Angular", llm.LastPrompt);
        Assert.Contains("@Component marca a classe", llm.LastPrompt);
    }

    // ─── PR 34g: reflexion retry ──────────────────────────────────────

    [Fact]
    public async Task Generate_WithPriorFailure_InjectsGuidanceIntoPrompt()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse }
;
        var sut = Build(llm, new AlwaysPassValidator(0));
        var feedback = new RetryFeedback(GenerationFailureReason.DistractorsPoor,
            "distratores fracos", AttemptNumber: 1);

        await sut.GenerateAsync(Claim, "Tema", feedback, default);

        Assert.Contains("AUTOCORREÇÃO", llm.LastPrompt);
        Assert.Contains("distratores", llm.LastPrompt);
    }

    [Fact]
    public async Task Generate_WithoutPriorFailure_NoGuidance()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse }
;
        var sut = Build(llm, new AlwaysPassValidator(0));

        await sut.GenerateAsync(Claim, "Tema", priorFailure: null, default);

        Assert.DoesNotContain("AUTOCORREÇÃO", llm.LastPrompt);
    }

    // ─── PR 34i: shape fallback no retry ──────────────────────────────

    [Fact]
    public async Task Generate_SchemaInvalidRetry_FallsBackFillBlankToMcq()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var sut = BuildWithShape(llm, QuestionShape.FillInTheBlank, new AlwaysPassValidator(0));
        var feedback = new RetryFeedback(GenerationFailureReason.SchemaInvalid,
            "Lacuna sem contexto à esquerda", AttemptNumber: 1);

        var result = await sut.GenerateAsync(Claim, "Tema", feedback, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(QuestionShape.MultipleChoice, result.Question!.Shape);
        Assert.Contains("RESPOSTA SUBSTANTIVA", llm.LastPrompt);  // marker MCQ prompt
    }

    [Fact]
    public async Task Generate_NonSchemaRetry_KeepsFillBlankShape()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var sut = BuildWithShape(llm, QuestionShape.FillInTheBlank, new AlwaysPassValidator(0));
        var feedback = new RetryFeedback(GenerationFailureReason.DistractorsPoor,
            "distratores fracos", AttemptNumber: 1);

        var result = await sut.GenerateAsync(Claim, "Tema", feedback, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(QuestionShape.FillInTheBlank, result.Question!.Shape);
    }

    // ─── PR 34a: shape selection ──────────────────────────────────────

    [Fact]
    public async Task Generate_PropagatesShapeFromRouter()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var sut = BuildWithShape(llm, QuestionShape.FillInTheBlank, new AlwaysPassValidator(0));

        var result = await sut.GenerateAsync(Claim, "Tema X");

        Assert.True(result.IsSuccess);
        Assert.Equal(QuestionShape.FillInTheBlank, result.Question!.Shape);
    }

    [Fact]
    public async Task Generate_DefaultsToMultipleChoice_WhenRouterSaysSo()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var sut = Build(llm, new AlwaysPassValidator(0));   // FixedShapeRouter default = MultipleChoice

        var result = await sut.GenerateAsync(Claim, "Tema X");

        Assert.True(result.IsSuccess);
        Assert.Equal(QuestionShape.MultipleChoice, result.Question!.Shape);
    }

    [Fact]
    public async Task Generate_FillBlankShape_UsesFillBlankPrompt()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var sut = BuildWithShape(llm, QuestionShape.FillInTheBlank, new AlwaysPassValidator(0));

        await sut.GenerateAsync(Claim, "Tema X");

        // Marker único do FillBlankPrompt (substring estável que NÃO aparece no MCQ).
        Assert.Contains("preencher a lacuna", llm.LastPrompt);
    }

    [Fact]
    public async Task Generate_MultipleChoiceShape_UsesMcqPrompt()
    {
        var llm = new StubLlm { NextResponse = ValidJsonResponse };
        var sut = Build(llm, new AlwaysPassValidator(0));

        await sut.GenerateAsync(Claim, "Tema X");

        Assert.Contains("RESPOSTA SUBSTANTIVA", llm.LastPrompt);
    }

    [Fact]
    public async Task Generate_CancellationPropagates()
    {
        var llm = new StubLlm { Throws = new OperationCanceledException() };
        var sut = Build(llm);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.GenerateAsync(Claim, "Title", cts.Token));
    }
}
