using System.Text;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded;

/// <summary>
/// Monta o prompt que o <see cref="LlmGroundedQuestionGenerator"/>
/// envia pro <c>ILlmInference</c>. Determinístico: mesmo input → mesmo
/// prompt (sem timestamps, sem aleatoriedade).
///
/// <para>Estrutura do prompt:</para>
/// <list type="number">
///   <item>Sistema instrutivo (role, restrições)</item>
///   <item>Trecho do conteúdo (única fonte permitida)</item>
///   <item>Claim alvo (a afirmação que deve ser testada)</item>
///   <item>Schema JSON exato esperado no output</item>
/// </list>
///
/// <para>A LLM recebe o prompt como string única (formato chat
/// completion-style); Ollama + Qwen 2.5 aceita esse padrão sem
/// system message separada.</para>
/// </summary>
internal static class PromptBuilder
{
    /// <summary>Limite de caracteres do chunk passado no prompt.
    /// Qwen 2.5 com num_ctx=4096 aceita ~12k chars de input + ~1k de
    /// output. Cap defensivo pra evitar truncar mid-sentence.</summary>
    private const int MaxChunkChars = 3_000;

    public static string Build(string contentTitle, ClaimCandidate claim)
    {
        var chunk = claim.ChunkText.Length > MaxChunkChars
            ? claim.ChunkText[..MaxChunkChars] + "…"
            : claim.ChunkText;

        var sb = new StringBuilder(capacity: chunk.Length + 1_500);

        sb.AppendLine("Você é gerador de questões educacionais em português brasileiro.");
        sb.AppendLine($"Tema: {contentTitle}");
        sb.AppendLine();
        sb.AppendLine("INSTRUÇÕES IMPORTANTES:");
        sb.AppendLine("- Use APENAS as informações do TRECHO abaixo como fonte. Não invente fatos.");
        sb.AppendLine("- Gere uma pergunta de múltipla escolha sobre o CONCEITO ALVO.");
        sb.AppendLine("- A resposta correta deve estar literalmente ou parafraseada no trecho.");
        sb.AppendLine("- Os 3 distratores devem ser plausíveis (do mesmo domínio) mas INCORRETOS.");
        sb.AppendLine("- NÃO repita a resposta correta na pergunta — o aluno deve pensar.");
        sb.AppendLine("- Escreva 1 frase de explicação justificando a resposta correta.");
        sb.AppendLine();
        sb.AppendLine("TRECHO (única fonte permitida):");
        sb.AppendLine("---");
        sb.AppendLine(chunk);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("CONCEITO ALVO (a afirmação que sua pergunta deve testar):");
        sb.AppendLine($"\"{claim.ClaimText}\"");
        sb.AppendLine();
        sb.AppendLine("Responda APENAS com um objeto JSON contendo exatamente estas chaves:");
        sb.AppendLine("{");
        sb.AppendLine("  \"prompt\": \"<a pergunta, sem revelar a resposta>\",");
        sb.AppendLine("  \"options\": [\"<opção 0>\", \"<opção 1>\", \"<opção 2>\", \"<opção 3>\"],");
        sb.AppendLine("  \"correctIndex\": <inteiro 0 a 3>,");
        sb.AppendLine("  \"explanation\": \"<justificativa de 1 frase>\"");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
