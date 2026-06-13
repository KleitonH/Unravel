using Unravel.Application.Forge.Llm;
using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Validators;

/// <summary>
/// Garante que a resposta correta tem suporte semântico no chunk-fonte.
/// Usa MiniLM (PR 18) pra calcular cosine similarity entre a resposta
/// e o chunk; rejeita se cosine &lt; threshold.
///
/// <para>Threshold default <b>0.55</b> (PR 31 decisão calibrada):
/// aceita paráfrases moderadas, rejeita respostas inventadas pela LLM
/// que não estejam no chunk. Calibração final acontece no PR 33 com
/// gold set.</para>
///
/// <para>Por que cosine e não exact match: o LLM pode reformular a
/// resposta ("o @Component marca a classe" vira "o decorator anota
/// a classe como componente") — exact match falharia, mas a relação
/// semântica está preservada. MiniLM detecta paráfrase com confiança
/// razoável em PT-BR.</para>
///
/// <para>Ordem 2 — depois de schema+leakage (que são string ops); este
/// faz embedding (~10-30ms por chamada, paga o overhead só se passou
/// nos baratos).</para>
///
/// <para>Quando <see cref="IEmbedder"/> não está registrado no DI
/// (<c>Embedding:Enabled=false</c>), este validador é dispensado pelo
/// gerador — descrito em <see cref="LlmGroundedQuestionGenerator"/>.
/// </para>
/// </summary>
public sealed class AnswerGroundednessValidator : IQuestionValidator
{
    private readonly IEmbedder _embedder;
    private readonly double    _threshold;

    public AnswerGroundednessValidator(IEmbedder embedder, double threshold = 0.55)
    {
        _embedder  = embedder;
        _threshold = threshold;
    }

    public int Order => 2;

    public (GenerationFailureReason Reason, string Detail)? Validate(
        GroundedQuestion question, ClaimCandidate claim)
    {
        // PR 34b — pra FillBlank usamos check literal (case-insensitive)
        // da resposta no chunk. Termos curtos (1-4 palavras) extraídos do
        // chunk costumam aparecer literais; cosine MiniLM em strings
        // muito curtas tem ruído alto e gera false-positive. Se o LLM
        // reformulou levemente (ex: "max_pool_size" → "maxPoolSize"),
        // a normalização básica abaixo ainda casa.
        if (question.Shape == QuestionShape.FillInTheBlank)
            return ValidateFillBlankLiteral(question, claim);

        var answer = question.Options[question.CorrectIndex];

        // PR 33e-bis — fallback de substring literal ANTES do cosine.
        // Diagnóstico PHP JIT: conteúdo técnico em PT-BR misturado com
        // jargão em inglês (tracing, opcache, jit_buffer_size) degrada
        // similaridade MiniLM mesmo quando a resposta cita literalmente
        // termos do chunk. Se o termo aparece literal (após normalização),
        // está "grounded" sem precisar de embedding — passamos direto.
        //
        // Isso preserva o cosine como gate semântico pra parafraseado puro
        // (que continua precisando >= threshold), mas evita rejeitar
        // respostas que referenciam termos técnicos verbatim.
        if (HasLiteralOverlap(answer, claim.ChunkText)) return null;

        var chunkVec  = _embedder.Encode(claim.ChunkText);
        var answerVec = _embedder.Encode(answer);

        var sim = IEmbedder.CosineSimilarity(chunkVec, answerVec);

        if (sim < _threshold)
            return (GenerationFailureReason.AnswerNotGrounded,
                $"Cosine(answer↔chunk)={sim:F3} < threshold {_threshold:F2}");

        return null;
    }

    /// <summary>
    /// PR 33e-bis — detecta se a resposta cita pedaço técnico do chunk
    /// literalmente. Considera "overlap suficiente" quando a maior
    /// palavra técnica da resposta (≥4 chars, alfanumérica) aparece
    /// também no chunk após normalização (case-insensitive, strip
    /// de underscores/traços/crases). Pra evitar match com palavras
    /// genéricas ("para", "como"), só conta tokens ≥4 chars.
    ///
    /// <para>Não substitui o cosine — só ATALHO pra casos onde o termo
    /// técnico aparece verbatim. Resposta sem overlap continua passando
    /// pelo gate semântico padrão.</para>
    /// </summary>
    private static bool HasLiteralOverlap(string answer, string chunk)
    {
        if (string.IsNullOrWhiteSpace(answer) || string.IsNullOrWhiteSpace(chunk))
            return false;

        var chunkNorm = Normalize(chunk);
        // Tokeniza a resposta em "palavras" de letras/dígitos.
        // Pega só tokens ≥4 chars (ignora artigos, conectivos, siglas
        // soltas demais como "JIT" cabe em 3 e fica fora; isso é OK
        // pois respostas tipicamente têm contexto adicional além da sigla).
        var tokens = System.Text.RegularExpressions.Regex
            .Matches(answer, @"[\p{L}\p{Nd}]{4,}")
            .Select(m => Normalize(m.Value))
            .Where(t => t.Length >= 4)
            .ToList();

        if (tokens.Count == 0) return false;

        // Considera "overlap" quando AO MENOS 1 token técnico significativo
        // aparece literal. 1 já basta porque o cosine continua sendo o gate
        // pra respostas puramente parafraseadas.
        return tokens.Any(t => chunkNorm.Contains(t));
    }

    /// <summary>PR 34b — grounding alternativo pra FillBlank: a resposta
    /// (termo extraído) precisa aparecer no chunk após normalização
    /// (lowercase + remove underscores/traços/crases pra casar variantes
    /// como <c>max_pool_size</c> ↔ <c>maxPoolSize</c>). Se o LLM
    /// inventou um termo que NÃO está no trecho, rejeita.</summary>
    private static (GenerationFailureReason, string)? ValidateFillBlankLiteral(
        GroundedQuestion question, ClaimCandidate claim)
    {
        var answer = question.Options is { Length: > 0 } opts
                     && question.CorrectIndex >= 0
                     && question.CorrectIndex < opts.Length
            ? opts[question.CorrectIndex] : null;

        if (string.IsNullOrWhiteSpace(answer)) return null; // schema cuida

        var normAnswer = Normalize(answer);
        var normChunk  = Normalize(claim.ChunkText ?? string.Empty);

        if (string.IsNullOrEmpty(normAnswer)) return null;
        if (normChunk.Contains(normAnswer)) return null;

        return (GenerationFailureReason.AnswerNotGrounded,
            $"FillBlank: termo correto '{Truncate(answer, 60)}' não aparece no chunk-fonte (mesmo após normalização)");
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            // tudo mais (espaço, _, -, `, @, ., :, etc.) descartado pra
            // casar maxPoolSize ↔ max_pool_size ↔ max-pool-size ↔ "max pool size"
        }
        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
