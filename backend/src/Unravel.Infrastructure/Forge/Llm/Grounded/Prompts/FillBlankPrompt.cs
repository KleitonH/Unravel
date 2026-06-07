using System.Text;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded.Prompts;

/// <summary>
/// Prompt fill-in-the-blank (PR 34a). Pede ao LLM uma sentença declarativa
/// do trecho com um termo-chave substituído por <c>_____</c> + 3 distratores
/// do mesmo tipo gramatical/conceitual.
///
/// <para><b>Diferenças vs MCQ</b>:</para>
/// <list type="bullet">
///   <item><c>prompt</c> é a sentença com lacuna (não uma pergunta).
///     UI mostra a frase inteira; opções viram chips de escolha.</item>
///   <item>Opções são <i>termos curtos</i> (≤4 palavras geralmente),
///     gramaticalmente compatíveis. Distratores são incorretos pelo
///     trecho mas plausíveis pra leitor (não-distratores óbvios).</item>
///   <item>A resposta correta é o termo removido — pode ser 1-4 palavras
///     (oposto da regra MCQ de frase completa).</item>
/// </list>
///
/// <para><b>Por que existe</b>: dá variedade visual ao quiz. Aluno
/// pratica reconhecimento de termo-chave em contexto, complementando o
/// MCQ que pratica conceito abstrato. Pesquisa de aprendizagem ativa
/// (Roediger, Karpicke) mostra que variar o formato aumenta retenção
/// vs. repetir o mesmo shape.</para>
///
/// <para><b>Schema esperado</b>:</para>
/// <code>
/// { "prompt": "O ... do @Component define o seletor CSS, conforme a especificação _____ do Angular.",
///   "options": ["component", "directive", "module", "pipe"],
///   "correctIndex": 0, "explanation": "..." }
/// </code>
/// </summary>
internal static class FillBlankPrompt
{
    private const int MaxChunkChars = 3_000;

    public static string Build(string contentTitle, ClaimCandidate claim)
    {
        var chunk = claim.ChunkText.Length > MaxChunkChars
            ? claim.ChunkText[..MaxChunkChars] + "…"
            : claim.ChunkText;

        var sb = new StringBuilder(capacity: chunk.Length + 3_000);

        sb.AppendLine("Você é gerador de questões \"preencher a lacuna\" em português brasileiro.");
        sb.AppendLine($"Tema: {contentTitle}");
        sb.AppendLine();
        sb.AppendLine("FORMATO DA PERGUNTA: você vai escolher UMA frase do TRECHO que afirma");
        sb.AppendLine("algo importante, identificar o TERMO-CHAVE dessa frase, e produzir uma");
        sb.AppendLine("versão da frase com esse termo substituído por exatamente \"_____\" (5 underscores).");
        sb.AppendLine();
        sb.AppendLine("REGRAS CRÍTICAS DE QUALIDADE:");
        sb.AppendLine();
        sb.AppendLine("1. TERMO-CHAVE: o termo removido deve ser CONCEITUAL e específico do tema —");
        sb.AppendLine("   nome de API, conceito técnico, palavra-chave de linguagem, identificador");
        sb.AppendLine("   de padrão. NÃO esconda palavras genéricas (\"o\", \"de\", \"componente\"");
        sb.AppendLine("   sem qualificador, \"sistema\"). Se o trecho não tem termo conceitual claro,");
        sb.AppendLine("   ESCOLHA OUTRA FRASE — não force.");
        sb.AppendLine();
        sb.AppendLine("2. LACUNA NO MEIO: posicione \"_____\" preferencialmente no meio da frase,");
        sb.AppendLine("   nunca como primeira ou última palavra. Aluno precisa de contexto antes E");
        sb.AppendLine("   depois pra raciocinar.");
        sb.AppendLine();
        sb.AppendLine("3. CONTEXTO PRESERVADO: a frase fora da lacuna deve continuar lendo de");
        sb.AppendLine("   forma natural. Mantenha pontuação, capitalização e conectivos exatamente");
        sb.AppendLine("   como no trecho. NÃO reformule a frase — só esconda o termo.");
        sb.AppendLine();
        sb.AppendLine("4. DISTRATORES SAME-TYPE: 3 alternativas do MESMO tipo gramatical/conceitual.");
        sb.AppendLine("   Se a resposta é nome de decorator, distratores são outros decorators.");
        sb.AppendLine("   Se é palavra-chave de linguagem, distratores são outras palavras-chave.");
        sb.AppendLine("   Comprimento similar (±50% caracteres). Todos plausíveis pra leitor que");
        sb.AppendLine("   conhece superficialmente o tema, todos errados pra essa frase específica.");
        sb.AppendLine();
        sb.AppendLine("5. EXPLICAÇÃO: 1 frase justificando por que o termo correto encaixa,");
        sb.AppendLine("   citando a parte do trecho que confirma.");
        sb.AppendLine();
        sb.AppendLine("EXEMPLO DE FILL-BLANK BOM:");
        sb.AppendLine("  TRECHO: \"O decorator @Component marca a classe como componente Angular");
        sb.AppendLine("   e define metadados como selector e template.\"");
        sb.AppendLine("  BOM: {");
        sb.AppendLine("    \"prompt\": \"O decorator _____ marca a classe como componente Angular");
        sb.AppendLine("       e define metadados como selector e template.\",");
        sb.AppendLine("    \"options\": [\"@Component\", \"@Directive\", \"@Injectable\", \"@NgModule\"],");
        sb.AppendLine("    \"correctIndex\": 0,");
        sb.AppendLine("    \"explanation\": \"@Component é o decorator citado no trecho como");
        sb.AppendLine("       responsável por marcar componentes Angular.\"");
        sb.AppendLine("  }");
        sb.AppendLine("  POR QUE É BOM: termo conceitual escondido (decorator específico), lacuna");
        sb.AppendLine("  no meio, frase ainda lê naturalmente, distratores são outros decorators");
        sb.AppendLine("  Angular (mesmo tipo, plausíveis, errados pra essa frase).");
        sb.AppendLine();
        sb.AppendLine("EXEMPLO RUIM (NÃO FAÇA):");
        sb.AppendLine("  RUIM: prompt=\"_____ marca a classe como componente Angular.\"");
        sb.AppendLine("       ← lacuna NO INÍCIO; sem contexto à esquerda.");
        sb.AppendLine("  RUIM: prompt=\"O decorator @Component marca a classe como _____.\"");
        sb.AppendLine("       options=[\"componente Angular\", \"módulo\", \"serviço\", \"pipe\"]");
        sb.AppendLine("       ← termo escondido é frase descritiva (não conceitual), não termo-chave.");
        sb.AppendLine("  RUIM: distractors=[\"@Component\", \"banana\", \"console.log\", \"undefined\"]");
        sb.AppendLine("       ← tipos misturados, não-plausíveis.");
        sb.AppendLine();
        sb.AppendLine("TRECHO (única fonte permitida):");
        sb.AppendLine("---");
        sb.AppendLine(chunk);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("AFIRMAÇÃO ALVO (use ela ou outra frase do trecho, se essa não tiver");
        sb.AppendLine("termo-chave conceitual claro):");
        sb.AppendLine($"\"{claim.ClaimText}\"");
        sb.AppendLine();
        sb.AppendLine("AGORA gere o fill-blank. Responda APENAS com um objeto JSON contendo");
        sb.AppendLine("exatamente estas chaves:");
        sb.AppendLine("{");
        sb.AppendLine("  \"prompt\": \"<sentença com _____ exatamente onde o termo foi removido>\",");
        sb.AppendLine("  \"options\": [\"<termo 0>\", \"<termo 1>\", \"<termo 2>\", \"<termo 3>\"],");
        sb.AppendLine("  \"correctIndex\": <inteiro 0 a 3>,");
        sb.AppendLine("  \"explanation\": \"<justificativa de 1 frase referenciando o trecho>\"");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
