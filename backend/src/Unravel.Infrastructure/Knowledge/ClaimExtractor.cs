using System.Text.RegularExpressions;
using Unravel.Application.Knowledge.Ports;
using Unravel.Infrastructure.Knowledge.Chunking;

namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// Extrator determinístico de claims (afirmações testáveis) de
/// markdown educativo. Implementa <see cref="IClaimExtractor"/>.
///
/// <para>Pipeline interno:</para>
/// <list type="number">
///   <item>Quebra markdown em chunks via <see cref="ChunkSegmenter"/>
///   (target ~400-800 tokens por chunk).</item>
///   <item>Pra cada chunk, segmenta o PlainText em sentenças.</item>
///   <item>Aplica filtros heurísticos pra descartar sentenças que
///   não são "claims testáveis" (curtas/longas demais, hedging,
///   imperativas/interrogativas, sem verbo principal).</item>
///   <item>Score cada sentença sobrevivente (presença de termo
///   definicional, ancoragem no heading, comprimento ideal).</item>
///   <item>Retorna top-N por chunk, ordenado por score desc.</item>
/// </list>
///
/// <para><b>Determinismo</b>: input idêntico → output idêntico.
/// Ordem dos chunks é a do source; ordem das claims dentro de um
/// chunk é por (Score desc, posição original asc) — empate desempata
/// pelo que vem antes no texto.</para>
///
/// <para>Não usa LLM, não chama I/O — puro CPU, thread-safe (sem
/// estado mutável após construção). Singleton candidato no DI.</para>
/// </summary>
public sealed class ClaimExtractor : IClaimExtractor
{
    // ── Configuração heurística ─────────────────────────────────────
    private const int MinWordCount = 8;
    private const int MaxWordCount = 35;

    /// <summary>
    /// Verbos principais aceitos como "núcleo de uma afirmação".
    /// Whitelist em vez de parser sintático completo — cobre 80% dos
    /// casos com 0% de dependência externa. Pode ser estendida via
    /// análise do gold set (PR 33).
    /// </summary>
    private static readonly HashSet<string> CoreVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        // Cópula e existência
        "é", "são", "está", "estão", "foi", "foram",
        // Definição / posse
        "tem", "têm", "possui", "possuem", "contém", "contêm",
        // Ação técnica frequente em texto de programação
        "define", "definem",
        "marca", "marcam",
        "permite", "permitem",
        "cria", "criam",
        "aceita", "aceitam",
        "recebe", "recebem",
        "retorna", "retornam",
        "controla", "controlam",
        "renderiza", "renderizam",
        "executa", "executam",
        "indica", "indicam",
        "representa", "representam",
        "serve", "servem",
        "evita", "evitam",
        "faz", "fazem",
        "ocorre", "ocorrem",
        "acontece", "acontecem",
        "garante", "garantem",
        "fornece", "fornecem",
        "implementa", "implementam",
        "estende", "estendem",
        "depende", "dependem",
        "exige", "exigem",
        "produz", "produzem",
        "consome", "consomem",
        "expõe", "expõem",
        "usa", "usam",
        "chama", "chamam",
        "associa", "associam",
        "registra", "registram",
        "injeta", "injetam",
        "dispara", "disparam",
        "configura", "configuram",
        "encapsula", "encapsulam",
        "habilita", "habilitam",
        "desabilita", "desabilitam",
    };

    /// <summary>
    /// Palavras de hedging — sinalizam afirmação fraca/probabilística.
    /// Sentenças com elas são descartadas (queremos só claims firmes
    /// que possam virar verdadeiro/falso ou múltipla escolha sem ambiguidade).
    /// </summary>
    private static readonly HashSet<string> HedgingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "talvez", "possivelmente", "provavelmente",
        "geralmente", "normalmente", "frequentemente",
        "às vezes", "as vezes", "ocasionalmente",
        "alguns", "algumas", "muitos", "muitas",
        "tipo", "tipos", "espécie",
        "pode", "podem", "poderia", "poderiam",
        "deveria", "deveriam",
        "depende",
    };

    /// <summary>
    /// Prefixos que indicam imperativo, pergunta retórica ou frase
    /// programática (não é claim). Match case-insensitive no início
    /// da sentença.
    /// </summary>
    private static readonly string[] BadPrefixes =
    {
        "o que ", "como ", "por que ", "por quê", "porque ",
        "vamos ", "veja ", "tente ", "experimente ", "considere ",
        "imagine ", "suponha ", "lembre", "note ", "observe ",
        "use ", "utilize ",
    };

    // Sentence-split aproximado: quebra em ". ", "! ", "? "
    // sem cortar abreviações comuns que terminam em ponto.
    private static readonly Regex SentenceSplit = new(
        @"(?<=[\.\!\?])\s+(?=[A-ZÁÉÍÓÚÂÊÔÇÃÕÀ])",
        RegexOptions.Compiled);

    // Token simples: palavras separadas por whitespace (acentos OK).
    private static readonly Regex TokenSplit = new(@"\s+", RegexOptions.Compiled);

    private readonly ChunkSegmenter _segmenter = new();

    public IReadOnlyList<ClaimCandidate> Extract(string markdownBody, int maxClaimsPerChunk = 5)
    {
        if (string.IsNullOrWhiteSpace(markdownBody) || maxClaimsPerChunk <= 0)
            return Array.Empty<ClaimCandidate>();

        var chunks = _segmenter.Segment(markdownBody);
        var all    = new List<ClaimCandidate>(chunks.Count * maxClaimsPerChunk);

        foreach (var chunk in chunks)
        {
            // Termos técnicos do chunk (heurística): palavras com caractere
            // não-alfabético (@, _, dígito) ou começando com maiúscula
            // dentro da sentença — proxy fraco mas útil pra "termo de domínio".
            var technicalTerms = ExtractTechnicalTerms(chunk.PlainText);

            var sentences = SentenceSplit.Split(chunk.PlainText)
                .Select(s => s.Trim().TrimEnd('.'))
                .Where(s => s.Length > 0)
                .ToList();

            var candidates = new List<(string text, int posIdx, double score)>();
            for (var i = 0; i < sentences.Count; i++)
            {
                var sent = sentences[i];
                if (!IsClaimCandidate(sent, out var words)) continue;
                var score = ScoreClaim(sent, words, technicalTerms, chunk.HeadingPath);
                candidates.Add((sent, i, score));
            }

            // Ordena por score desc, desempate por posição original asc.
            foreach (var c in candidates
                         .OrderByDescending(c => c.score)
                         .ThenBy(c => c.posIdx)
                         .Take(maxClaimsPerChunk))
            {
                all.Add(new ClaimCandidate(chunk.Index, chunk.PlainText, c.text + ".", Math.Round(c.score, 3)));
            }
        }

        return all;
    }

    /// <summary>
    /// Aplica todos os filtros que descartam sentenças não-claim.
    /// Retorna o array de palavras se passou, ou null se foi descartada.
    /// </summary>
    private static bool IsClaimCandidate(string sentence, out string[] words)
    {
        words = TokenSplit.Split(sentence)
            .Where(t => t.Length > 0)
            .ToArray();

        // 1) Comprimento (palavras)
        if (words.Length < MinWordCount || words.Length > MaxWordCount) return false;

        // 2) Prefixo proibido (imperativo / interrogativo / instrucional)
        var lower = sentence.TrimStart().ToLowerInvariant();
        if (BadPrefixes.Any(p => lower.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;

        // 3) Termina com '?' → interrogativa (já filtrada pelo split em geral,
        //    mas pode passar se vier sem espaço depois)
        if (sentence.EndsWith('?')) return false;

        // 4) Hedging
        foreach (var w in words)
        {
            var clean = w.Trim('.', ',', ';', ':', '(', ')').ToLowerInvariant();
            if (HedgingWords.Contains(clean)) return false;
        }

        // 5) Verbo principal — exige pelo menos uma forma da whitelist
        var hasCoreVerb = words.Any(w =>
            CoreVerbs.Contains(w.Trim('.', ',', ';', ':', '(', ')')));
        if (!hasCoreVerb) return false;

        return true;
    }

    /// <summary>
    /// Score em [0, 1] (heurístico, não calibrado). Combina sinais:
    /// comprimento ideal (~12-22 palavras), padrão "X é Y" definicional,
    /// presença de termo técnico do chunk, e cobertura do heading.
    /// </summary>
    private static double ScoreClaim(string sentence, string[] words, HashSet<string> technical, string headingPath)
    {
        var score = 0.5; // base

        // Comprimento ideal — bell-curve simples em torno de 15 palavras
        var idealDist = Math.Abs(words.Length - 15);
        score += 0.15 * Math.Max(0, 1 - idealDist / 12.0);

        // Padrão definicional "X é Y" / "X são Y" / "X marca/define/permite Y"
        // Detecta presença de verbo cópula nas primeiras 8 palavras (sujeito curto).
        var earlyVerb = words.Take(Math.Min(8, words.Length))
            .Any(w => IsDefinitionalVerb(w.Trim('.', ',', ';', ':', '(', ')')));
        if (earlyVerb) score += 0.15;

        // Termo técnico presente
        foreach (var term in technical)
        {
            if (sentence.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.1;
                break; // 1 bônus, evitar dupla contagem
            }
        }

        // Cobre alguma palavra do heading path
        if (!string.IsNullOrEmpty(headingPath))
        {
            var headingTokens = TokenSplit.Split(headingPath.ToLowerInvariant())
                .Where(t => t.Length >= 4)
                .ToHashSet();
            var sentLower = sentence.ToLowerInvariant();
            if (headingTokens.Any(t => sentLower.Contains(t)))
                score += 0.1;
        }

        return Math.Clamp(score, 0, 1);
    }

    private static bool IsDefinitionalVerb(string w) =>
        w.Equals("é", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("são", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("define", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("definem", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("marca", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("permite", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("permitem", StringComparison.OrdinalIgnoreCase) ||
        w.Equals("representa", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Heurística simples pra extrair "termos técnicos" do chunk —
    /// palavras com características que sugerem identificador/API:
    /// CamelCase, snake_case, com @, com (), ou com dígitos.
    /// Usado só pra scoring; falsos positivos são tolerados.
    /// </summary>
    private static HashSet<string> ExtractTechnicalTerms(string plainText)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawTok in TokenSplit.Split(plainText))
        {
            var tok = rawTok.Trim('.', ',', ';', ':', '(', ')', '"', '\'', '!');
            if (tok.Length < 3 || tok.Length > 30) continue;
            // PascalCase / camelCase com pelo menos uma maiúscula interna
            if (tok.Length > 1 && char.IsUpper(tok[0]) &&
                tok.Skip(1).Any(char.IsUpper)) { terms.Add(tok); continue; }
            // snake_case ou contém dígito
            if (tok.Contains('_') || tok.Any(char.IsDigit)) { terms.Add(tok); continue; }
            // contém @ (decorator) ou cifrão
            if (tok.Contains('@') || tok.Contains('$')) { terms.Add(tok); }
        }
        return terms;
    }
}
