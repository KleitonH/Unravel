using System.Text;
using System.Text.RegularExpressions;

namespace Unravel.Infrastructure.Knowledge.Chunking;

/// <summary>
/// Quebra o markdown de um Content em chunks com tamanho-alvo.
/// O ponto-de-quebra natural é o <c>##</c> (H2) — cada seção H2 vira
/// um chunk. Se a seção for grande demais, sub-quebra em parágrafos
/// preservando atomicidade (não corta no meio de uma frase).
///
/// <para><b>Target:</b> ~400-800 tokens / ~1500-3000 caracteres
/// (1 token ≈ 4 chars em PT). Não é hard cap — uma seção pequena
/// não é forçada a juntar com outra.</para>
///
/// <para>Cada chunk preserva:</para>
/// <list type="bullet">
///   <item><b>RawMarkdown</b> — original com formatação (inclui blocos
///   de código), pra ser passado ao LLM como contexto rico</item>
///   <item><b>PlainText</b> — versão limpa (via MarkdownStripper) pra
///   sentence-split no ClaimExtractor</item>
///   <item><b>HeadingPath</b> — caminho de headings ("Anatomia > Decorator"),
///   útil pra debug e pra contextualizar o LLM</item>
/// </list>
/// </summary>
internal sealed class ChunkSegmenter
{
    private const int TargetCharsMin = 1_500;
    private const int TargetCharsMax = 3_000;

    // Captura headings ## (H2) e ### (H3) preservando o nível.
    // (?m) multiline — ^ casa início de cada linha.
    private static readonly Regex HeadingRegex = new(
        @"^(?<level>#{2,3})\s+(?<text>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public IReadOnlyList<ContentChunk> Segment(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return Array.Empty<ContentChunk>();

        // 1) Quebra inicial por H2 — cada seção é candidata a chunk.
        var sections = SplitByH2(markdown);

        // 2) Refinar: seções grandes viram múltiplos chunks (split por
        //    parágrafo); pequenas continuam atômicas.
        var chunks = new List<ContentChunk>();
        foreach (var section in sections)
        {
            if (section.RawMarkdown.Length <= TargetCharsMax)
            {
                chunks.Add(MakeChunk(section.HeadingPath, section.RawMarkdown, chunks.Count));
                continue;
            }
            // Split por parágrafo (linha em branco), acumulando até atingir
            // o target. Mantém heading no primeiro sub-chunk.
            var paragraphs = section.RawMarkdown.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
            var buffer     = new StringBuilder();
            foreach (var p in paragraphs)
            {
                if (buffer.Length + p.Length > TargetCharsMax && buffer.Length >= TargetCharsMin)
                {
                    chunks.Add(MakeChunk(section.HeadingPath, buffer.ToString(), chunks.Count));
                    buffer.Clear();
                }
                if (buffer.Length > 0) buffer.Append("\n\n");
                buffer.Append(p);
            }
            if (buffer.Length > 0)
                chunks.Add(MakeChunk(section.HeadingPath, buffer.ToString(), chunks.Count));
        }

        return chunks;
    }

    /// <summary>
    /// Quebra o markdown nas linhas de H2. Mantém H3 dentro da seção
    /// como subdivisão (vira parte do HeadingPath quando relevante).
    /// Conteúdo antes do primeiro H2 é tratado como seção "intro"
    /// (HeadingPath vazio).
    /// </summary>
    private static List<RawSection> SplitByH2(string markdown)
    {
        var matches = HeadingRegex.Matches(markdown);
        if (matches.Count == 0)
            return new List<RawSection> { new(string.Empty, markdown.Trim()) };

        var sections   = new List<RawSection>();
        var currentH2  = string.Empty;
        var prevEnd    = 0;
        string? pendingHeading = null;

        // Conteúdo antes do primeiro heading
        if (matches[0].Index > 0)
        {
            var intro = markdown.Substring(0, matches[0].Index).Trim();
            if (intro.Length > 0) sections.Add(new RawSection(string.Empty, intro));
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var m     = matches[i];
            var level = m.Groups["level"].Value.Length; // 2 ou 3
            var text  = m.Groups["text"].Value.Trim();

            // Se o último heading foi um H2 sem conteúdo (heading imediatamente
            // seguido de outro heading), aceita; vamos só fechar.
            if (pendingHeading != null)
            {
                var bodyStart = prevEnd;
                var bodyEnd   = m.Index;
                AppendSection(sections, currentH2, pendingHeading, markdown, bodyStart, bodyEnd);
                pendingHeading = null;
            }

            if (level == 2)
            {
                currentH2 = text;
                pendingHeading = text;
                prevEnd = m.Index + m.Length;
            }
            else // H3
            {
                pendingHeading = string.IsNullOrEmpty(currentH2) ? text : $"{currentH2} > {text}";
                prevEnd = m.Index + m.Length;
            }
        }

        // Última seção
        if (pendingHeading != null)
            AppendSection(sections, currentH2, pendingHeading, markdown, prevEnd, markdown.Length);

        return sections;
    }

    private static void AppendSection(List<RawSection> dest, string h2, string headingPath, string md, int start, int end)
    {
        var body = md.Substring(start, end - start).Trim();
        if (body.Length == 0) return;
        // Prepend o heading no corpo, pra preservar contexto no chunk.
        var withHeading = headingPath.Contains(" > ")
            ? $"### {headingPath.Split(" > ")[^1]}\n\n{body}"
            : $"## {headingPath}\n\n{body}";
        dest.Add(new RawSection(headingPath, withHeading));
    }

    private static ContentChunk MakeChunk(string headingPath, string rawMarkdown, int index)
    {
        var plain = MarkdownStripper.Strip(rawMarkdown);
        return new ContentChunk(index, headingPath, rawMarkdown.Trim(), plain);
    }

    private readonly record struct RawSection(string HeadingPath, string RawMarkdown);
}

/// <summary>
/// Um pedaço atômico do <c>Content.Body</c>. Identificado pelo
/// <see cref="Index"/> sequencial (0-based) — estável dentro de um
/// dado markdown, então pode ser usado como referência cross-table
/// (challenge.SourceChunkIndex futuramente).
/// </summary>
public sealed record ContentChunk(
    int    Index,
    string HeadingPath,
    string RawMarkdown,
    string PlainText);
