using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

namespace Unravel.Infrastructure.Forge.Llm.Grounded;

/// <summary>
/// Orquestrador da geração grounded (PR 31). Recebe um
/// <see cref="ClaimCandidate"/> (do PR 29), monta o prompt
/// (<see cref="PromptBuilder"/>), chama o LLM (<see cref="ILlmInference"/>
/// do PR 30), parseia o JSON, e roda a cadeia de validadores
/// (<see cref="IQuestionValidator"/>).
///
/// <para><b>1 tentativa por claim</b> (decisão calibrada — yield ~70%).
/// Se quiser retry, é o orchestrator de mais alto nível (PR 32) que
/// decide quando re-enfileirar com seed nova.</para>
///
/// <para>Telemetria: cada falha incrementa contador específico por
/// <see cref="GenerationFailureReason"/> via logging estruturado. O
/// counter OTel correspondente vai entrar quando integrarmos com
/// UnravelMetrics no PR 32.</para>
///
/// <para><b>Sem retry interno</b>: temperature &gt; 0 gera variância
/// natural; um claim que falhou desse jeito provavelmente vai falhar
/// de novo. Vale mais a pena descartar e usar o próximo claim do
/// mesmo chunk ou ir pro próximo conteúdo.</para>
/// </summary>
public sealed class LlmGroundedQuestionGenerator : IGroundedQuestionGenerator
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        // LLMs ocasionalmente devolvem chaves com casing diferente do
        // schema instruído (mesmo com format=json). Tolerar é barato.
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas         = true,
    };

    private readonly ILlmInference _llm;
    private readonly IQuestionValidator[] _validators;
    private readonly ILogger<LlmGroundedQuestionGenerator> _log;

    public LlmGroundedQuestionGenerator(
        ILlmInference llm,
        IEnumerable<IQuestionValidator> validators,
        ILogger<LlmGroundedQuestionGenerator> log)
    {
        _llm        = llm;
        _validators = validators.OrderBy(v => v.Order).ToArray();
        _log        = log;
    }

    public async Task<GroundedGenerationResult> GenerateAsync(
        ClaimCandidate claim,
        string contentTitle,
        CancellationToken ct = default)
    {
        var prompt = PromptBuilder.Build(contentTitle, claim);

        string? raw;
        try
        {
            raw = await _llm.CompleteAsync(prompt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log.LogError(ex, "LLM threw during generation");
            return GroundedGenerationResult.Fail(GenerationFailureReason.LlmEmpty, ex.Message);
        }

        if (string.IsNullOrWhiteSpace(raw))
            return GroundedGenerationResult.Fail(GenerationFailureReason.LlmEmpty, "LLM returned empty");

        GroundedQuestion question;
        try
        {
            var parsed = JsonSerializer.Deserialize<LlmOutput>(raw, JsonOpts);
            if (parsed is null)
                return GroundedGenerationResult.Fail(GenerationFailureReason.JsonParseError, "Deserialized to null");

            question = new GroundedQuestion(
                Prompt:           parsed.Prompt ?? string.Empty,
                Options:          parsed.Options ?? Array.Empty<string>(),
                CorrectIndex:     parsed.CorrectIndex,
                Explanation:      parsed.Explanation,
                SourceChunkIndex: claim.ChunkIndex);
        }
        catch (JsonException ex)
        {
            // Format=json deveria evitar isso, mas o modelo pode às vezes
            // devolver JSON inválido (string com aspas mal-escapadas, etc.)
            _log.LogDebug(ex, "Failed to parse LLM output as JSON: {Raw}", Truncate(raw, 200));
            return GroundedGenerationResult.Fail(GenerationFailureReason.JsonParseError, ex.Message);
        }

        foreach (var validator in _validators)
        {
            var failure = validator.Validate(question, claim);
            if (failure is not null)
            {
                // Loga em Info (não Debug) pra diagnóstico: gerador real
                // produziu o JSON, validator rejeitou — útil pra calibrar
                // thresholds. Reduzir pra Debug quando o pool maturar.
                // PR 33e+: guarda quando correctIndex é inválido (Schema
                // pode falhar JUSTAMENTE por correctIndex fora; antes
                // acessar Options[CorrectIndex] crashava o logging).
                var answerSafe = (question.Options is { Length: > 0 } opts
                                  && question.CorrectIndex >= 0
                                  && question.CorrectIndex < opts.Length)
                    ? Truncate(opts[question.CorrectIndex], 80)
                    : "(invalid index)";
                _log.LogInformation(
                    "Question rejected by {Validator}: {Reason} ({Detail}). " +
                    "Prompt was: \"{Prompt}\" / Answer was: \"{Answer}\"",
                    validator.GetType().Name, failure.Value.Reason, failure.Value.Detail,
                    Truncate(question.Prompt ?? string.Empty, 120),
                    answerSafe);
                return GroundedGenerationResult.Fail(failure.Value.Reason, failure.Value.Detail);
            }
        }

        return GroundedGenerationResult.Ok(question);
    }

    // ── helpers ─────────────────────────────────────────────────────

    /// <summary>Schema esperado do output do LLM.
    /// Internal pra ser visível ao teste via InternalsVisibleTo.</summary>
    internal sealed class LlmOutput
    {
        [JsonPropertyName("prompt")]       public string?  Prompt       { get; set; }
        [JsonPropertyName("options")]      public string[]? Options     { get; set; }
        [JsonPropertyName("correctIndex")] public int      CorrectIndex { get; set; }
        [JsonPropertyName("explanation")]  public string?  Explanation  { get; set; }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
