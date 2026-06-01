using System.Text.RegularExpressions;
using Markdig;

namespace Unravel.Infrastructure.Knowledge.Chunking;

/// <summary>
/// Converte markdown em texto-corrido limpo, descartando blocos
/// estruturais que não contribuem pra extração de claims:
/// blocos de código (```), inline code (`),
/// imagens, e marcadores de lista. Mantém o texto natural das frases.
///
/// <para><b>Decisão</b>: blocos de código são descartados (não viram
/// claims). Eles continuam disponíveis no <i>chunk completo</i> que
/// o LLM recebe como contexto (PR 31), mas não geram sentenças que
/// possam ser perguntadas — código não é "afirmação testável".</para>
///
/// <para>Idempotente e thread-safe (sem estado mutável).</para>
/// </summary>
internal static class MarkdownStripper
{
    // ```lang\n...\n``` — multiline. Greedy fechamento controlado.
    private static readonly Regex FencedCodeBlock = new(
        @"```[\w-]*\n[\s\S]*?\n```",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // `inline code` — não-greedy. Não casa três crases (já tratadas acima).
    private static readonly Regex InlineCode = new(
        @"(?<!`)`[^`\n]+?`(?!`)",
        RegexOptions.Compiled);

    // Captura linhas de heading (## / ###) ANTES do Markdig comê-los.
    // Pós-Markdig, headings viram texto puro sem `##` mas também sem
    // pontuação no final — fica grudado com o próximo parágrafo no
    // sentence-split (sentence splitter só quebra em .!?). Forçamos
    // ponto-final no fim do heading pra criar um boundary explícito.
    private static readonly Regex HeadingLine = new(
        @"^(?<hash>#{1,6})\s+(?<text>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Compacta whitespace: múltiplos espaços/tabs → 1 espaço; preserva quebras
    // duplas (parágrafo) mas reduz triplas+ pra duplas.
    private static readonly Regex MultiSpace = new(@"[ \t]+", RegexOptions.Compiled);
    private static readonly Regex MultiNewline = new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>
    /// Strip todo: retorna texto puro pronto pra sentence-split.
    /// </summary>
    public static string Strip(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        // 1) Remover code blocks ANTES de mandar pro Markdig — caso contrário
        //    ele renderiza o conteúdo como texto também, poluindo.
        var withoutCode = FencedCodeBlock.Replace(markdown, "");

        // 2) Forçar ponto-final no fim de cada heading. Sem isso, "##
        //    Para que serve\n\nO componente é..." vira pós-Markdig
        //    "Para que serve\nO componente é..." (sem ##, mas grudado);
        //    sentence splitters baseados em .!? não conseguem separar.
        //    Com este patch, vira "Para que serve.\n\nO componente é...".
        //    Skip se heading já termina com pontuação.
        withoutCode = HeadingLine.Replace(withoutCode, m =>
        {
            var text = m.Groups["text"].Value.TrimEnd();
            var needsPeriod = text.Length > 0 && !".!?:".Contains(text[^1]);
            return $"{m.Groups["hash"].Value} {text}{(needsPeriod ? "." : "")}";
        });

        // 3) Markdig converte tudo restante (headings, listas, links, ênfase,
        //    blockquotes) pra texto plano. Inline code que sobrou vira texto;
        //    removemos depois (não queremos `inject()` como claim).
        var plainText = Markdown.ToPlainText(withoutCode);

        // 3) Inline code restante (Markdig pode preservar ` em alguns casos)
        plainText = InlineCode.Replace(plainText, " ");

        // 4) Normalização de whitespace.
        plainText = MultiSpace.Replace(plainText, " ");
        plainText = MultiNewline.Replace(plainText, "\n\n");

        return plainText.Trim();
    }
}
