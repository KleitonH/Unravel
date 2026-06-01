using Unravel.Application.Knowledge.Ports;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Gera uma pergunta de múltipla escolha <b>grounded</b> num
/// (chunk, claim) extraídos pelo <see cref="IClaimExtractor"/>.
/// "Grounded" significa: a resposta correta deve estar fundamentada
/// no chunk-fonte; distratores devem ser plausíveis mas distintos;
/// pergunta não pode vazar a resposta.
///
/// <para>Implementação canônica (PR 31) compõe um prompt
/// determinístico, chama <see cref="ILlmInference"/> com saída
/// JSON forçada, e roda a pergunta gerada por uma cadeia de
/// validators. Se qualquer validador rejeita, retorna
/// <c>(null, failure)</c> — o orchestrator decide se descarta ou
/// re-tenta.</para>
///
/// <para><b>Latência</b>: 5-15s por chamada (Ollama+GPU). Não usar
/// no caminho síncrono do usuário — só em batch (cron noturno ou
/// fila admin).</para>
/// </summary>
public interface IGroundedQuestionGenerator
{
    Task<GroundedGenerationResult> GenerateAsync(
        ClaimCandidate claim,
        string         contentTitle,
        CancellationToken ct = default);
}

/// <summary>Resultado da geração — sucesso traz a pergunta validada;
/// falha traz o motivo (pra telemetria e debug).</summary>
public sealed record GroundedGenerationResult(
    GroundedQuestion?      Question,
    GenerationFailureReason FailureReason,
    string?                FailureDetail)
{
    public bool IsSuccess => Question is not null;

    public static GroundedGenerationResult Ok(GroundedQuestion q) =>
        new(q, GenerationFailureReason.None, null);

    public static GroundedGenerationResult Fail(GenerationFailureReason r, string detail) =>
        new(null, r, detail);
}

/// <summary>Pergunta de MCQ gerada e validada.
///
/// <para><b>Estável vs efêmera</b>: esse record é o "valor verdadeiro"
/// pós-validação. O <c>GeneratedChallenge</c> persistido no DB tem
/// campos a mais (Strategy, EstimatedDifficulty, ServedCount, etc.)
/// — o mapper aplica fora.</para>
/// </summary>
public sealed record GroundedQuestion(
    string   Prompt,
    string[] Options,
    int      CorrectIndex,
    string?  Explanation,
    int      SourceChunkIndex);

/// <summary>Tipos de falha enumerados pra telemetria por bucket
/// (entender qual validador é o gargalo, ajustar prompt/threshold).</summary>
public enum GenerationFailureReason
{
    None = 0,
    /// <summary>LLM não retornou nada (timeout, daemon down).</summary>
    LlmEmpty,
    /// <summary>Output da LLM não é JSON válido (raro com format=json).</summary>
    JsonParseError,
    /// <summary>JSON não tem os campos esperados ou tem valores inválidos.</summary>
    SchemaInvalid,
    /// <summary>O prompt vazou parte/totalidade da resposta correta.</summary>
    AnswerLeakage,
    /// <summary>A resposta correta não tem ancoragem semântica no chunk.</summary>
    AnswerNotGrounded,
    /// <summary>Distratores muito similares à resposta ou ao chunk.</summary>
    DistractorsPoor,
}
