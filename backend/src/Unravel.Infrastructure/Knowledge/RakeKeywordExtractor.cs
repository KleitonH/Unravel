using System.Text.RegularExpressions;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;

namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// RAKE (Rapid Automatic Keyword Extraction) — Rose et al. 2010.
///
/// <para>Por que RAKE e não TF-IDF puro: TF-IDF exige corpus para o IDF;
/// num cold-start de trilha só temos um punhado de Contents. RAKE é
/// intra-documento, não precisa de corpus, é O(n) e empiricamente captura
/// termos compostos ("máquina de Turing", "injeção de SQL") tão bem quanto
/// abordagens muito mais caras. Boas perguntas-cloze precisam disso.</para>
///
/// <para>Pipeline determinístico:</para>
/// <list type="number">
///   <item>Quebra o texto em "frases candidatas" pelos delimitadores de pontuação.</item>
///   <item>Em cada frase, quebra em sub-frases pelos stopwords — o que sobra
///   entre dois stopwords é uma "frase candidata" (1+ tokens).</item>
///   <item>Cada token recebe score = grau(token) / frequência(token), onde
///   grau é a soma dos tamanhos das frases em que ele aparece.</item>
///   <item>Score da frase candidata = soma dos scores dos tokens.</item>
///   <item>Termos retornados são as frases candidatas canonizadas
///   (chaveadas por stem); duplicatas são fundidas somando scores.</item>
/// </list>
/// </summary>
public sealed class RakeKeywordExtractor : IKeywordExtractor
{
    private static readonly Regex TokenizeWord =
        new(@"[\p{L}\p{Nd}][\p{L}\p{Nd}\-_+#]*", RegexOptions.Compiled);

    public IReadOnlyList<Keyword> Extract(string text, int topN = 12)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<Keyword>();

        var candidates = ExtractCandidatePhrases(text);
        if (candidates.Count == 0) return Array.Empty<Keyword>();

        var (degree, frequency) = ComputeWordStats(candidates);

        var wordScore = new Dictionary<string, double>(degree.Count);
        foreach (var (word, deg) in degree)
            wordScore[word] = (double)deg / frequency[word];

        // Score de cada frase candidata (mantém forma original do 1º token-líder)
        // mas chaveia por stem para fundir variantes ("programação"/"programar").
        var phraseByKey = new Dictionary<string, (string display, double score)>();
        foreach (var phrase in candidates)
        {
            var score = phrase.Sum(t => wordScore.GetValueOrDefault(TextNormalizer.CanonicalKey(t), 0));
            if (score <= 0) continue;

            var key     = string.Join(' ', phrase.Select(TextNormalizer.CanonicalKey));
            var display = string.Join(' ', phrase).ToLowerInvariant();

            if (phraseByKey.TryGetValue(key, out var existing))
                phraseByKey[key] = (existing.display, existing.score + score);
            else
                phraseByKey[key] = (display, score);
        }

        // Penaliza unigramas comuns de baixo peso e dá leve boost a bigramas/trigrams,
        // que tendem a ser termos técnicos coesos ("redes neurais", "injeção de dependência").
        return phraseByKey
            .Select(kv =>
            {
                var arity = kv.Key.Count(c => c == ' ') + 1;
                var boost = arity switch { 1 => 1.0, 2 => 1.15, 3 => 1.25, _ => 1.10 };
                return new Keyword(kv.Value.display, kv.Value.score * boost);
            })
            .OrderByDescending(k => k.Score)
            .ThenBy(k => k.Term, StringComparer.Ordinal)   // tie-break determinístico
            .Take(topN)
            .ToList();
    }

    private static List<string[]> ExtractCandidatePhrases(string text)
    {
        var phrases = new List<string[]>();
        foreach (var sentence in text.Split(StopwordsPt.PhraseDelimiters,
                                            StringSplitOptions.RemoveEmptyEntries))
        {
            var current = new List<string>();
            foreach (Match m in TokenizeWord.Matches(sentence))
            {
                var token = m.Value;
                var canonical = TextNormalizer.CanonicalKey(token);

                if (StopwordsPt.Set.Contains(token) || StopwordsPt.Set.Contains(canonical) || canonical.Length < 2)
                {
                    if (current.Count > 0) { phrases.Add(current.ToArray()); current = new List<string>(); }
                }
                else
                {
                    current.Add(token);
                }
            }
            if (current.Count > 0) phrases.Add(current.ToArray());
        }
        return phrases;
    }

    private static (Dictionary<string,int> degree, Dictionary<string,int> frequency)
        ComputeWordStats(List<string[]> phrases)
    {
        var degree    = new Dictionary<string, int>();
        var frequency = new Dictionary<string, int>();

        foreach (var phrase in phrases)
        {
            var len = phrase.Length;
            foreach (var word in phrase)
            {
                var key = TextNormalizer.CanonicalKey(word);
                frequency[key] = frequency.GetValueOrDefault(key) + 1;
                // RAKE original: degree(w) inclui o próprio token (len-1 co-ocorrências + 1)
                degree[key]    = degree.GetValueOrDefault(key)    + len;
            }
        }
        return (degree, frequency);
    }
}
