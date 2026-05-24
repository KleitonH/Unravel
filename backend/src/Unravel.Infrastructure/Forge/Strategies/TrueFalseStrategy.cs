using System.Text.RegularExpressions;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Infrastructure.Forge.Strategies;

/// <summary>
/// V/F com mutação: pega uma afirmação do body (correta) e gera uma
/// variante com mutação controlada (falsa). Pergunta: "Qual afirmação está
/// correta?" entre as duas.
///
/// <para>Por ser uma pergunta entre 2 opções, o QualityGate exige pelo menos
/// 3 alternativas, então geramos 4: a verdadeira + 3 falsas com mutações
/// distintas (negação, troca de número, troca de termo por distrator).</para>
///
/// <para><b>Mutações</b>:</para>
/// <list type="bullet">
///   <item><b>Negação</b>: insere "não" antes do primeiro verbo conhecido.</item>
///   <item><b>Troca de número</b>: troca número por outro próximo (10 → 100).</item>
///   <item><b>Troca de termo</b>: substitui a keyword principal por um distrator.</item>
/// </list>
/// </summary>
public sealed class TrueFalseStrategy : IChallengeStrategy
{
    public ForgeStrategy Kind => ForgeStrategy.TrueFalse;

    private static readonly Regex SentenceSplitter =
        new(@"(?<=[.!?])\s+(?=[A-ZÁÉÍÓÚÂÊÔÃÕÇ])", RegexOptions.Compiled);

    private static readonly Regex NumberPattern =
        new(@"\b\d+\b", RegexOptions.Compiled);

    /// <summary>Lista enxuta de verbos comuns onde "não" se insere de forma
    /// não-acrobática. Não tenta ser completo; só o suficiente para que a
    /// mutação produza sentença legível.</summary>
    private static readonly string[] InjectableVerbs =
    {
        "é", "são", "está", "estão", "tem", "têm", "pode", "deve", "permite",
        "usa", "usam", "funciona", "funcionam", "representa", "consiste",
    };

    private readonly IDistractorPicker _distractors;

    public TrueFalseStrategy(IDistractorPicker distractors) => _distractors = distractors;

    public IReadOnlyList<GeneratedChallengeDraft> Generate(
        Content content, Topic topic, KnowledgeGraph graph, int maxDrafts)
    {
        if (string.IsNullOrWhiteSpace(content.Body) || topic.Keywords.Count == 0)
            return Array.Empty<GeneratedChallengeDraft>();

        var sentences = SentenceSplitter.Split(content.Body)
                                        .Select(s => s.Trim())
                                        .Where(s => s.Length >= 30 && s.Length <= 180)
                                        .Where(s => InjectableVerbs.Any(v =>
                                            Regex.IsMatch(s, $@"\b{v}\b", RegexOptions.IgnoreCase)))
                                        .ToList();

        var drafts = new List<GeneratedChallengeDraft>();
        var usedSentences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sentence in sentences)
        {
            if (drafts.Count >= maxDrafts) break;
            if (!usedSentences.Add(sentence)) continue;

            var falsies = new List<string>();

            // Mutação 1: negação.
            var negated = TryNegate(sentence);
            if (negated is not null && !falsies.Contains(negated)) falsies.Add(negated);

            // Mutação 2: troca de número.
            var numberSwap = TrySwapNumber(sentence);
            if (numberSwap is not null && !falsies.Contains(numberSwap)) falsies.Add(numberSwap);

            // Mutação 3: troca de keyword principal por distrator.
            var hitKeyword = topic.Keywords
                .Select(k => new { k.Term, Match = FindOccurrence(sentence, k.Term) })
                .FirstOrDefault(x => x.Match is not null);
            if (hitKeyword is not null)
            {
                var d = _distractors.Pick(hitKeyword.Match!, topic, graph, count: 1).FirstOrDefault();
                if (d is not null)
                {
                    var swapped = sentence.Replace(hitKeyword.Match!, d);
                    if (!falsies.Contains(swapped)) falsies.Add(swapped);
                }
            }

            // Garantir 3 falsies: se faltar, pula esta sentença (QualityGate
            // exigiria duplicatas).
            if (falsies.Count < 3) continue;
            falsies = falsies.Take(3).ToList();

            var all = falsies.Append(sentence)
                             .OrderBy(o => TextNormalizer.CanonicalKey(o), StringComparer.Ordinal)
                             .ToList();
            var correctIndex = all.IndexOf(sentence);

            drafts.Add(new GeneratedChallengeDraft(
                SourceTopicId:       topic.Id,
                SourceContentId:     content.Id,
                Strategy:            ForgeStrategy.TrueFalse,
                Prompt:              "Qual das afirmações abaixo está correta segundo o material da trilha?",
                Options:             all,
                CorrectIndex:        correctIndex,
                Explanation:         $"A afirmação correta é a original do material; as demais introduzem distorções (negação, troca de número ou de termo).",
                EstimatedDifficulty: Math.Clamp(topic.DifficultyScore + 0.05, 0.05, 0.95)));
        }

        return drafts;
    }

    // ── Mutações ─────────────────────────────────────────────────────

    private static string? TryNegate(string sentence)
    {
        foreach (var verb in InjectableVerbs)
        {
            var rx = new Regex($@"\b({verb})\b", RegexOptions.IgnoreCase);
            var m  = rx.Match(sentence);
            if (m.Success)
                return sentence[..m.Index] + "não " + sentence[m.Index..];
        }
        return null;
    }

    private static string? TrySwapNumber(string sentence)
    {
        var m = NumberPattern.Match(sentence);
        if (!m.Success) return null;

        var n = int.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture);
        // Mutação determinística e sempre distinta: 0→1, 1→2, n>=2 → n*10.
        var mutated = n switch { 0 => 1, 1 => 2, _ => n * 10 };
        if (mutated == n) mutated = n + 1;
        return sentence[..m.Index] + mutated.ToString(System.Globalization.CultureInfo.InvariantCulture)
             + sentence[(m.Index + m.Length)..];
    }

    private static string? FindOccurrence(string sentence, string keyword)
    {
        var parts = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var keyCanon = string.Join(' ', parts.Select(TextNormalizer.CanonicalKey));
        var tokens = Regex.Matches(sentence, @"[\p{L}\p{Nd}][\p{L}\p{Nd}\-_+#]*");
        for (var i = 0; i <= tokens.Count - parts.Length; i++)
        {
            var windowTokens = Enumerable.Range(i, parts.Length).Select(j => tokens[j].Value).ToList();
            var windowCanon  = string.Join(' ', windowTokens.Select(TextNormalizer.CanonicalKey));
            if (windowCanon == keyCanon) return string.Join(' ', windowTokens);
        }
        return null;
    }
}
