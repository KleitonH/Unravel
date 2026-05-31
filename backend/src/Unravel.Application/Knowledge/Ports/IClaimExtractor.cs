namespace Unravel.Application.Knowledge.Ports;

/// <summary>
/// Extração determinística de "afirmações testáveis" (atomic claims) a
/// partir do corpo markdown de um <c>Content</c>. Não usa LLM — só NLP
/// (segmentação por estrutura markdown + filtros de sentenças).
///
/// <para>Cada <see cref="ClaimCandidate"/> é uma sentença declarativa
/// curta, sem hedging, com sujeito e verbo principal claros, ancorada
/// num chunk específico do conteúdo. Esses pares
/// <c>(chunk, claim)</c> alimentam o
/// <c>LlmGroundedQuestionGenerator</c> (PR 31): o LLM recebe o chunk
/// como evidência e o claim como alvo, e devolve uma pergunta cuja
/// resposta correta deve estar literalmente ou parafraseada no chunk.
/// </para>
///
/// <para>Sem dependência de DbContext nem LLM — é puro CPU. Idempotente:
/// mesmo texto → mesma lista de candidatos, na mesma ordem.</para>
/// </summary>
public interface IClaimExtractor
{
    /// <summary>
    /// Extrai claims do corpo markdown.
    /// </summary>
    /// <param name="markdownBody">Conteúdo cru do <c>Content.Body</c>
    /// (sem frontmatter). Pode incluir blocos de código, listas e headers.</param>
    /// <param name="maxClaimsPerChunk">Cap por chunk pra evitar saturar
    /// um único trecho. Default 5.</param>
    IReadOnlyList<ClaimCandidate> Extract(string markdownBody, int maxClaimsPerChunk = 5);
}

/// <summary>
/// Um candidato a claim extraído de um chunk específico.
///
/// <para><b>ChunkIndex</b> permite que o gerador de perguntas mantenha
/// referência estável ao trecho-fonte (pra validação posterior:
/// "a resposta está nesse chunk?").</para>
///
/// <para><b>ChunkText</b> é o texto-limpo do chunk (sem markdown),
/// usado como contexto pro LLM. Não é o source original.</para>
///
/// <para><b>ClaimText</b> é a sentença em si, o "alvo" da pergunta.</para>
///
/// <para><b>Score</b> é uma heurística de "qualidade do claim" (0..1) —
/// quanto maior, mais provável que vire uma boa pergunta. Combina
/// comprimento ideal, presença de termos técnicos do chunk, e
/// estrutura sujeito-verbo-objeto. Não é probabilidade calibrada,
/// é só pra ordenar candidatos.</para>
/// </summary>
public sealed record ClaimCandidate(
    int    ChunkIndex,
    string ChunkText,
    string ClaimText,
    double Score);
