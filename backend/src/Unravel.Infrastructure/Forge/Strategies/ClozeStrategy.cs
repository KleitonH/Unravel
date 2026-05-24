using System.Text.RegularExpressions;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Infrastructure.Forge.Strategies;

/// <summary>
/// Cloze (preencher lacuna): identifica uma sentença informativa do
/// <see cref="Content.Body"/>, remove o termo de maior peso (uma keyword
/// do topic que aparece na sentença) e gera múltipla escolha com a resposta
/// correta + distratores plausíveis.
///
/// <para><b>Heurística da sentença</b>: maior soma de keywords do topic
/// presentes. Sentenças com mais keywords são mais centrais ao conceito.</para>
///
/// <para><b>Heurística do termo removido</b>: maior peso entre as keywords
/// do topic que aparecem na sentença. Vai do termo mais representativo,
/// não de qualquer substantivo.</para>
/// </summary>
public sealed class ClozeStrategy : IChallengeStrategy
{
    public ForgeStrategy Kind => ForgeStrategy.Cloze;

    private static readonly Regex SentenceSplitter =
        new(@"(?<=[.!?])\s+(?=[A-ZÁÉÍÓÚÂÊÔÃÕÇ])", RegexOptions.Compiled);

    private readonly IDistractorPicker _distractors;

    public ClozeStrategy(IDistractorPicker distractors) => _distractors = distractors;

    public IReadOnlyList<GeneratedChallengeDraft> Generate(
        Content content, Topic topic, KnowledgeGraph graph, int maxDrafts)
    {
        if (string.IsNullOrWhiteSpace(content.Body) || topic.Keywords.Count == 0)
            return Array.Empty<GeneratedChallengeDraft>();

        var sentences = SentenceSplitter.Split(content.Body)
                                        .Select(s => s.Trim())
                                        .Where(s => s.Length >= 30 && s.Length <= 220)
                                        .ToList();

        var drafts = new List<GeneratedChallengeDraft>();
        var usedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sentence in sentences)
        {
            if (drafts.Count >= maxDrafts) break;

            // Encontra a keyword do topic com maior peso presente nessa sentença.
            var hit = topic.Keywords
                .Select(k => new { k.Term, k.Score, Match = FindOccurrence(sentence, k.Term) })
                .Where(x => x.Match is not null)
                .Where(x => !usedTerms.Contains(x.Term))
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (hit is null) continue;

            var originalForm = hit.Match!;
            usedTerms.Add(hit.Term);

            var distractors = _distractors.Pick(originalForm, topic, graph, count: 3);
            if (distractors.Count < 3) continue;

            // Constrói a frase com lacuna mantendo pontuação.
            var blanked = ReplaceOnce(sentence, originalForm, "_____");

            // Embaralhamento determinístico: ordena options pelo hash do termo
            // canonizado — same input, same order; nunca aleatório.
            var allOptions = distractors.Append(originalForm)
                                        .OrderBy(o => TextNormalizer.CanonicalKey(o), StringComparer.Ordinal)
                                        .ToList();
            var correctIndex = allOptions.IndexOf(originalForm);

            drafts.Add(new GeneratedChallengeDraft(
                SourceTopicId:       topic.Id,
                SourceContentId:     content.Id,
                Strategy:            ForgeStrategy.Cloze,
                Prompt:              $"Complete a frase: \"{blanked}\"",
                Options:             allOptions,
                CorrectIndex:        correctIndex,
                Explanation:         $"O termo correto é \"{originalForm}\", extraído do material da trilha.",
                EstimatedDifficulty: Math.Clamp(topic.DifficultyScore - 0.05, 0.05, 0.95)));
        }

        return drafts;
    }

    /// <summary>Localiza a ocorrência da keyword (potencialmente flexionada)
    /// na sentença e devolve a forma exata encontrada. Compara por chave
    /// canonical (sem diacrítico, com stem leve) para casar variantes.</summary>
    private static string? FindOccurrence(string sentence, string keyword)
    {
        var keyParts  = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (keyParts.Length == 0) return null;
        var keyCanon  = string.Join(' ', keyParts.Select(TextNormalizer.CanonicalKey));

        // Janela do tamanho do termo (em palavras) sobre os tokens da sentença.
        var tokens = Regex.Matches(sentence, @"[\p{L}\p{Nd}][\p{L}\p{Nd}\-_+#]*");
        for (var i = 0; i <= tokens.Count - keyParts.Length; i++)
        {
            var windowTokens = Enumerable.Range(i, keyParts.Length).Select(j => tokens[j].Value).ToList();
            var windowCanon  = string.Join(' ', windowTokens.Select(TextNormalizer.CanonicalKey));
            if (windowCanon == keyCanon)
                return string.Join(' ', windowTokens);
        }
        return null;
    }

    private static string ReplaceOnce(string source, string oldValue, string newValue)
    {
        var idx = source.IndexOf(oldValue, StringComparison.Ordinal);
        return idx < 0 ? source : source[..idx] + newValue + source[(idx + oldValue.Length)..];
    }
}
