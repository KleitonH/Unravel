using System.Text.RegularExpressions;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Knowledge;
using Unravel.Infrastructure.Knowledge.Chunking;

namespace Unravel.Infrastructure.Forge.Strategies;

/// <summary>
/// Definição inversa: detecta padrões "X é/são/consiste em Y" no body do
/// Content e gera "O que é X?" com Y correto + Y's de outros conceitos.
///
/// <para><b>Vantagem sobre Cloze</b>: a pergunta vira mais natural ("O que
/// é uma closure?" vs "Complete a frase: '___ captura variáveis...'"),
/// porque parte de uma definição explícita no texto. Limitação: só
/// funciona quando o autor do Content escreveu na forma definicional.</para>
/// </summary>
public sealed class DefinitionStrategy : IChallengeStrategy
{
    public ForgeStrategy Kind => ForgeStrategy.Definition;

    /// <summary>
    /// Padrões definicionais PT-BR. Capturas:
    /// <list type="bullet">
    ///   <item>$1 = termo (concept being defined)</item>
    ///   <item>$2 = definição (the predicate)</item>
    /// </list>
    /// Ordenados do mais específico ao mais genérico — first match wins.
    /// </summary>
    private static readonly Regex[] Patterns =
    {
        new(@"\b([\p{Lu}][\p{L}\d\-]*(?:\s+[\p{L}\d\-]+)*)\s+(?:é|são)\s+(?:um(?:a)?(?:s)?\s+)?(?<def>[^.;:!?]{15,180})",
            RegexOptions.Compiled),
        new(@"\b([\p{Lu}][\p{L}\d\-]*(?:\s+[\p{L}\d\-]+)*)\s+(?:consiste em|refere-se a|representa)\s+(?<def>[^.;:!?]{15,180})",
            RegexOptions.Compiled),
        new(@"\b([\p{Lu}][\p{L}\d\-]*(?:\s+[\p{L}\d\-]+)*)\s+(?:serve para|tem como objetivo)\s+(?<def>[^.;:!?]{15,180})",
            RegexOptions.Compiled),
    };

    private readonly IDistractorPicker _distractors;

    public DefinitionStrategy(IDistractorPicker distractors) => _distractors = distractors;

    public IReadOnlyList<GeneratedChallengeDraft> Generate(
        Content content, Topic topic, KnowledgeGraph graph, int maxDrafts)
    {
        if (string.IsNullOrWhiteSpace(content.Body)) return Array.Empty<GeneratedChallengeDraft>();

        // Bug 1: strip markdown antes de aplicar regex de definições.
        // Sem isso, "## Para que serve\nO componente é..." matchava
        // "Para que serve\nO componente" como "termo" → prompt "O que é
        // Para que serve\n\nO componente?". MarkdownStripper resolve.
        var plain = MarkdownStripper.Strip(content.Body);

        var drafts    = new List<GeneratedChallengeDraft>();
        var usedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in Patterns)
        {
            if (drafts.Count >= maxDrafts) break;

            foreach (Match m in pattern.Matches(plain))
            {
                if (drafts.Count >= maxDrafts) break;

                var term       = m.Groups[1].Value.Trim();
                var definition = m.Groups["def"].Value.Trim().TrimEnd('.', ',', ';');

                if (term.Length is < 3 or > 60) continue;
                if (definition.Length is < 15 or > 180) continue;
                if (!usedTerms.Add(term)) continue;

                // Distratores são definições de OUTROS topics da trilha.
                // Como não temos cache de definições por topic, usamos as
                // próprias keywords dos outros topics como "respostas
                // alternativas" — menos preciso mas funciona.
                var rawDistractors = _distractors.Pick(definition, topic, graph, count: 3);
                if (rawDistractors.Count < 3) continue;

                // Os distratores precisam ler como "definições" plausíveis,
                // não como termos soltos. Prefixamos um conector neutro.
                var dressedDistractors = rawDistractors
                    .Select(d => char.ToLowerInvariant(d[0]) + d[1..])
                    .ToList();

                var allOptions = dressedDistractors
                    .Append(definition[0..1].ToLowerInvariant() + definition[1..])
                    .OrderBy(o => TextNormalizer.CanonicalKey(o), StringComparer.Ordinal)
                    .ToList();

                var correctText  = definition[0..1].ToLowerInvariant() + definition[1..];
                var correctIndex = allOptions.IndexOf(correctText);

                drafts.Add(new GeneratedChallengeDraft(
                    SourceTopicId:       topic.Id,
                    SourceContentId:     content.Id,
                    Strategy:            ForgeStrategy.Definition,
                    Prompt:              $"O que é {term}?",
                    Options:             allOptions,
                    CorrectIndex:        correctIndex,
                    Explanation:         $"Segundo o material da trilha, {term} {(pattern == Patterns[0] ? "é" : "consiste em")} {definition}.",
                    EstimatedDifficulty: Math.Clamp(topic.DifficultyScore, 0.05, 0.95)));
            }
        }

        return drafts;
    }
}
