using System.Text;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Prompts;

/// <summary>
/// Prompt MCQ canônico (PR 33e). Extraído do antigo
/// <c>PromptBuilder.Build</c> sem alteração funcional — yield baseline
/// 69% no eval real, então mantemos texto, exemplos e regras na íntegra.
///
/// <para>Schema esperado no output (forçado via <c>format=json</c> do
/// Ollama / OpenAI):</para>
/// <code>
/// { "prompt": "...", "options": ["...","...","...","..."],
///   "correctIndex": 0..3, "explanation": "..." }
/// </code>
/// </summary>
internal static class MultipleChoicePrompt
{
    private const int MaxChunkChars = 3_000;

    public static string Build(string contentTitle, ClaimCandidate claim)
    {
        var chunk = claim.ChunkText.Length > MaxChunkChars
            ? claim.ChunkText[..MaxChunkChars] + "…"
            : claim.ChunkText;

        var sb = new StringBuilder(capacity: chunk.Length + 3_000);

        sb.AppendLine("Você é gerador de questões educacionais em português brasileiro.");
        sb.AppendLine($"Tema: {contentTitle}");
        sb.AppendLine();
        sb.AppendLine("REGRAS CRÍTICAS DE QUALIDADE (PR 33e — calibrado em eval real):");
        sb.AppendLine();
        sb.AppendLine("1. FIDELIDADE: a resposta correta deve aparecer literalmente ou em");
        sb.AppendLine("   paráfrase muito próxima no TRECHO. Nada de inferir, nada de");
        sb.AppendLine("   conhecimento externo. Se você não acha base no trecho, GERE OUTRA");
        sb.AppendLine("   pergunta sobre o claim, não invente.");
        sb.AppendLine();
        sb.AppendLine("2. NÃO VAZAMENTO: a pergunta NÃO pode conter as palavras-chave da resposta");
        sb.AppendLine("   correta. O aluno precisa raciocinar — se a resposta está no enunciado,");
        sb.AppendLine("   não é avaliação. Use pergunta indireta (\"o que acontece quando...\",");
        sb.AppendLine("   \"qual o resultado de...\", \"qual a função de...\") em vez de direta");
        sb.AppendLine("   (\"como funciona X?\" onde X é a resposta).");
        sb.AppendLine();
        sb.AppendLine("3. RESPOSTA SUBSTANTIVA: a resposta correta não pode ser apenas 1-2");
        sb.AppendLine("   palavras (\"@Component\", \"selector\"). Deve ser uma FRASE COMPLETA");
        sb.AppendLine("   explicando o conceito (8-25 palavras). Aluno escolhe entendimento,");
        sb.AppendLine("   não vocabulário.");
        sb.AppendLine();
        sb.AppendLine("4. DISTRATORES: 3 alternativas plausíveis (do mesmo domínio do tema),");
        sb.AppendLine("   incorretas pelo trecho. Não \"banana\", não \"nenhuma das anteriores\",");
        sb.AppendLine("   não cópias quase-literais da resposta.");
        sb.AppendLine();
        sb.AppendLine("5. EXPLICAÇÃO: 1 frase justificando por que a correta é correta,");
        sb.AppendLine("   conectando ao trecho.");
        sb.AppendLine();
        sb.AppendLine("EXEMPLO DE PERGUNTA RUIM (NÃO FAÇA):");
        sb.AppendLine("  TRECHO: \"O decorator @Component marca a classe como componente Angular.\"");
        sb.AppendLine("  RUIM   : prompt=\"O que o decorator @Component faz?\"  ← VAZA \"@Component\"");
        sb.AppendLine("  RUIM   : prompt=\"Qual a função do @Component?\"      ← VAZA também");
        sb.AppendLine("  RUIM   : correctAnswer=\"@Component\"                  ← MUITO CURTO, é vocabulário");
        sb.AppendLine();
        sb.AppendLine("EXEMPLO DE PERGUNTA BOA (FAÇA ASSIM):");
        sb.AppendLine("  TRECHO: \"O decorator @Component marca a classe como componente Angular.\"");
        sb.AppendLine("  BOA: {");
        sb.AppendLine("    \"prompt\": \"Como uma classe TypeScript comum se torna reconhecida como");
        sb.AppendLine("       um componente pelo framework Angular?\",");
        sb.AppendLine("    \"options\": [");
        sb.AppendLine("      \"Através da aplicação do decorator @Component à classe\",");
        sb.AppendLine("      \"Implementando a interface IAngularComponent\",");
        sb.AppendLine("      \"Estendendo a classe abstrata BaseComponent\",");
        sb.AppendLine("      \"Registrando manualmente no array bootstrap do AppModule\"");
        sb.AppendLine("    ],");
        sb.AppendLine("    \"correctIndex\": 0,");
        sb.AppendLine("    \"explanation\": \"@Component é o decorator que sinaliza ao Angular");
        sb.AppendLine("       que a classe é um componente, conforme descrito no trecho.\"");
        sb.AppendLine("  }");
        sb.AppendLine("  POR QUE É BOA: prompt não menciona @Component (não vaza); resposta é frase");
        sb.AppendLine("  completa explicativa; distratores são plausíveis (existem padrões parecidos");
        sb.AppendLine("  em outros frameworks) mas errados pra Angular.");
        sb.AppendLine();
        sb.AppendLine("TRECHO (única fonte permitida):");
        sb.AppendLine("---");
        sb.AppendLine(chunk);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("CONCEITO ALVO (a afirmação do trecho que sua pergunta deve testar):");
        sb.AppendLine($"\"{claim.ClaimText}\"");
        sb.AppendLine();
        sb.AppendLine("AGORA gere a sua pergunta. Responda APENAS com um objeto JSON contendo");
        sb.AppendLine("exatamente estas chaves:");
        sb.AppendLine("{");
        sb.AppendLine("  \"prompt\": \"<pergunta indireta, sem revelar a resposta>\",");
        sb.AppendLine("  \"options\": [\"<opção 0>\", \"<opção 1>\", \"<opção 2>\", \"<opção 3>\"],");
        sb.AppendLine("  \"correctIndex\": <inteiro 0 a 3>,");
        sb.AppendLine("  \"explanation\": \"<justificativa de 1 frase conectando ao trecho>\"");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
