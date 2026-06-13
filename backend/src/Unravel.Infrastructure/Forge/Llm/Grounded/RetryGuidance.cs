using System.Text;
using Unravel.Application.Forge.Ports;

namespace Unravel.Infrastructure.Forge.Llm.Grounded;

/// <summary>
/// PR 34g — gera o bloco de "reflexion" anexado ao prompt em re-tentativas.
/// Recebe o motivo da rejeição anterior e instrui o LLM a corrigir aquele
/// erro específico.
///
/// <para>Por que funciona: as falhas que sobram após calibração (PR 34f)
/// são erros estocásticos que o LLM corrige quando sabe o que errou —
/// "distratores muito parecidos" → ele gera mais distintos; "vazou a
/// resposta" → ele reformula a pergunta. Retry cego (só temperatura)
/// repetia o mesmo erro; retry informado quebra o loop.</para>
///
/// <para>Mensagens são <b>específicas por <see cref="GenerationFailureReason"/></b>:
/// guidance genérico ("tente de novo") não move a agulha. Cada reason tem
/// uma correção acionável.</para>
/// </summary>
internal static class RetryGuidance
{
    public static string Build(RetryFeedback feedback)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("════════════════════════════════════════════════");
        sb.AppendLine($"⚠ AUTOCORREÇÃO (tentativa {feedback.AttemptNumber + 1})");
        sb.AppendLine("Sua geração ANTERIOR foi REJEITADA pela validação automática.");
        sb.AppendLine("Corrija o problema específico abaixo e gere uma versão NOVA e melhor:");
        sb.AppendLine();
        sb.AppendLine(GuidanceFor(feedback.Reason));
        if (!string.IsNullOrWhiteSpace(feedback.Detail))
        {
            sb.AppendLine();
            sb.AppendLine($"Detalhe técnico da rejeição: {feedback.Detail}");
        }
        sb.AppendLine("════════════════════════════════════════════════");
        return sb.ToString();
    }

    private static string GuidanceFor(GenerationFailureReason reason) => reason switch
    {
        GenerationFailureReason.AnswerLeakage =>
            "PROBLEMA: o enunciado VAZOU a resposta correta (continha palavras-chave dela).\n" +
            "CORRIJA: reformule a pergunta de forma INDIRETA, sem citar nenhum termo da " +
            "resposta. Use \"qual o resultado de...\", \"o que acontece quando...\", " +
            "\"qual a função de...\" em vez de mencionar o conceito diretamente.",

        GenerationFailureReason.AnswerNotGrounded =>
            "PROBLEMA: a resposta correta NÃO tem base clara no TRECHO fornecido.\n" +
            "CORRIJA: escolha como resposta correta algo que apareça LITERALMENTE ou em " +
            "paráfrase muito próxima no trecho. Não infira nem use conhecimento externo — " +
            "cite o que o material realmente diz.",

        GenerationFailureReason.DistractorsPoor =>
            "PROBLEMA: os distratores (alternativas erradas) eram fracos — muito parecidos " +
            "com a resposta, ou óbvios/absurdos demais.\n" +
            "CORRIJA: gere 3 distratores PLAUSÍVEIS do mesmo domínio técnico, todos " +
            "claramente INCORRETOS pelo trecho mas que um aluno desatento poderia escolher. " +
            "Não use 'nenhuma das anteriores', não copie a resposta com leves mudanças, " +
            "não use opções absurdas (banana, etc.).",

        GenerationFailureReason.SchemaInvalid =>
            "PROBLEMA: a estrutura do JSON estava inválida (faltou campo, opções " +
            "duplicadas, índice fora do range, ou — em fill-in-the-blank — a lacuna " +
            "_____ ficou no início/fim sem contexto suficiente).\n" +
            "CORRIJA: garanta EXATAMENTE 4 opções distintas, correctIndex entre 0-3, " +
            "prompt e explicação preenchidos. Se for fill-in-the-blank, posicione _____ " +
            "no MEIO da frase com pelo menos 2 palavras de contexto antes e depois.",

        GenerationFailureReason.JsonParseError =>
            "PROBLEMA: a saída anterior não era JSON válido.\n" +
            "CORRIJA: responda APENAS com o objeto JSON puro, sem texto antes/depois, " +
            "sem markdown, com aspas devidamente escapadas.",

        _ =>
            "PROBLEMA: a geração anterior não passou na validação de qualidade.\n" +
            "CORRIJA: siga rigorosamente todas as regras do prompt e gere uma versão melhor.",
    };
}
