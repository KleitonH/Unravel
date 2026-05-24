using System.Text.RegularExpressions;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Infrastructure.Forge.Strategies;

/// <summary>
/// Detecta pares "Termo: definição" ou "Termo — definição" / "Termo - definição"
/// (3 a 4 pares) no body e gera "Qual associação está correta?" — cada
/// alternativa é o conjunto de pares; 1 correto + 3 com definições trocadas.
///
/// <para><b>Por que 3-4 pares</b>: 2 é trivial; 5+ vira soletrar.</para>
/// </summary>
public sealed class MatchStrategy : IChallengeStrategy
{
    public ForgeStrategy Kind => ForgeStrategy.Match;

    private static readonly Regex PairLine =
        new(@"^[ \t]*[-*•]?[ \t]*([\p{Lu}][\p{L}\d\-_ ]{1,40}?)[ \t]*[:\-—–][ \t]+(.{8,100}?)[ \t]*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

    public IReadOnlyList<GeneratedChallengeDraft> Generate(
        Content content, Topic topic, KnowledgeGraph graph, int maxDrafts)
    {
        if (string.IsNullOrWhiteSpace(content.Body)) return Array.Empty<GeneratedChallengeDraft>();

        var pairs = ExtractPairs(content.Body);
        if (pairs.Count is < 3 or > 4) return Array.Empty<GeneratedChallengeDraft>();

        // Garante unicidade de termos e definições (sem dupes).
        if (pairs.Select(p => p.Term).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pairs.Count) return Array.Empty<GeneratedChallengeDraft>();
        if (pairs.Select(p => p.Definition).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pairs.Count) return Array.Empty<GeneratedChallengeDraft>();

        var correctMapping = pairs;
        var wrongMappings  = WrongMappings(pairs);
        if (wrongMappings.Count < 3) return Array.Empty<GeneratedChallengeDraft>();

        var correctText = FormatMapping(correctMapping);
        var alts = wrongMappings.Take(3).Select(FormatMapping)
                                .Append(correctText)
                                .OrderBy(TextNormalizer.CanonicalKey, StringComparer.Ordinal)
                                .ToList();

        var draft = new GeneratedChallengeDraft(
            SourceTopicId:       topic.Id,
            SourceContentId:     content.Id,
            Strategy:            ForgeStrategy.Match,
            Prompt:              "Qual conjunto de associações está correto, segundo o material da trilha?",
            Options:             alts,
            CorrectIndex:        alts.IndexOf(correctText),
            Explanation:         $"As associações corretas são: {correctText}.",
            EstimatedDifficulty: Math.Clamp(topic.DifficultyScore + 0.05, 0.10, 0.95));

        return new[] { draft };
    }

    private static List<(string Term, string Definition)> ExtractPairs(string body)
    {
        var matches = PairLine.Matches(body);
        var pairs   = new List<(string Term, string Definition)>(matches.Count);
        foreach (Match m in matches)
        {
            var term = m.Groups[1].Value.Trim();
            var def  = m.Groups[2].Value.Trim().TrimEnd('.', ',', ';');
            if (term.Length < 2 || def.Length < 5) continue;
            // Evita capturar definições que pareçam mais um título ou cabeçalho.
            if (term.Length > 40 || def.Length > 100) continue;
            pairs.Add((term, def));
        }
        return pairs.Take(4).ToList();
    }

    /// <summary>Gera mapeamentos errados via permutações das definições
    /// (mantém os termos na ordem original).</summary>
    private static List<List<(string Term, string Definition)>>
        WrongMappings(List<(string Term, string Definition)> correct)
    {
        var result = new List<List<(string, string)>>();
        var defs   = correct.Select(p => p.Definition).ToList();

        // Permutação 1: reverso
        var p1 = correct.Select((p, i) => (p.Term, defs[correct.Count - 1 - i])).ToList();
        if (!Same(p1, correct)) result.Add(p1);

        // Permutação 2: rotaciona 1 (def[i] → def[(i+1) % n])
        var p2 = correct.Select((p, i) => (p.Term, defs[(i + 1) % correct.Count])).ToList();
        if (!Same(p2, correct) && !result.Any(x => Same(x, p2))) result.Add(p2);

        // Permutação 3: swap dos dois primeiros termos
        if (correct.Count >= 2)
        {
            var p3 = new List<(string Term, string Definition)>(correct);
            p3[0] = (correct[0].Term, correct[1].Definition);
            p3[1] = (correct[1].Term, correct[0].Definition);
            if (!Same(p3, correct) && !result.Any(x => Same(x, p3))) result.Add(p3);
        }

        // Permutação 4 (fallback): swap do par central
        if (result.Count < 3 && correct.Count >= 3)
        {
            var p4 = new List<(string Term, string Definition)>(correct);
            p4[1] = (correct[1].Term, correct[2].Definition);
            p4[2] = (correct[2].Term, correct[1].Definition);
            if (!Same(p4, correct) && !result.Any(x => Same(x, p4))) result.Add(p4);
        }

        return result;
    }

    private static bool Same(
        IReadOnlyList<(string Term, string Definition)> a,
        IReadOnlyList<(string Term, string Definition)> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (!string.Equals(a[i].Definition, b[i].Definition, StringComparison.Ordinal))
                return false;
        return true;
    }

    private static string FormatMapping(IEnumerable<(string Term, string Definition)> pairs) =>
        string.Join("; ", pairs.Select(p => $"{p.Term} → {p.Definition}"));
}
