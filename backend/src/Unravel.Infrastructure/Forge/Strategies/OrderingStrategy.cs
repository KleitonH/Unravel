using System.Text.RegularExpressions;
using Unravel.Application.Forge.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Infrastructure.Forge.Strategies;

/// <summary>
/// Detecta listas no <see cref="Content.Body"/> (numeradas ou bullets) com
/// 3 a 6 itens, e gera "Qual é a ordem correta?" como múltipla escolha
/// — cada alternativa é uma permutação da sequência.
///
/// <para><b>Por que limitar a 3-6 itens</b>: menos que 3 não há ordem a
/// aprender; mais que 6 a UI fica ilegível como alternativa de texto.</para>
///
/// <para><b>Permutações</b>: precisamos de 3 erradas + 1 correta = 4. O
/// embaralhamento é determinístico (hash da própria sequência), garantindo
/// que mesmo input produz mesmas alternativas — exigência do
/// QualityGate e do snapshot do PR 7.</para>
/// </summary>
[Obsolete("PR 34e — template-based; substituido pelo pipeline LlmGrounded (PR 31+). " +
          "Nao registrado no DI por default; ativa via Forge:UseLegacyStrategies=true se necessario.")]
public sealed class OrderingStrategy : IChallengeStrategy
{
    public ForgeStrategy Kind => ForgeStrategy.Ordering;

    /// <summary>Casa linhas que comecem com "1.", "2.", "-", "*". Captura
    /// o índice (quando houver) e o texto restante (até o fim da linha).</summary>
    private static readonly Regex ListItem =
        new(@"^[ \t]*(?:(\d{1,2})[\.)]|[-*•])[ \t]+(.+?)[ \t]*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

    public IReadOnlyList<GeneratedChallengeDraft> Generate(
        Content content, Topic topic, KnowledgeGraph graph, int maxDrafts)
    {
        if (string.IsNullOrWhiteSpace(content.Body)) return Array.Empty<GeneratedChallengeDraft>();

        var items = ExtractList(content.Body);
        if (items.Count is < 3 or > 6) return Array.Empty<GeneratedChallengeDraft>();

        var permutations = ThreeDeterministicShuffles(items);
        // se não conseguimos 3 permutações ≠ original, pula (ordem é trivial).
        if (permutations.Count < 3) return Array.Empty<GeneratedChallengeDraft>();

        var correct = FormatSequence(items);
        var alts = permutations.Select(FormatSequence)
                               .Append(correct)
                               .OrderBy(TextNormalizer.CanonicalKey, StringComparer.Ordinal)
                               .ToList();

        var draft = new GeneratedChallengeDraft(
            SourceTopicId:       topic.Id,
            SourceContentId:     content.Id,
            Strategy:            ForgeStrategy.Ordering,
            Prompt:              "Qual é a ordem correta dos passos abaixo, segundo o material da trilha?",
            Options:             alts,
            CorrectIndex:        alts.IndexOf(correct),
            Explanation:         $"A ordem correta é: {correct}.",
            EstimatedDifficulty: Math.Clamp(topic.DifficultyScore + 0.10, 0.10, 0.95));

        return new[] { draft };
    }

    private static List<string> ExtractList(string body)
    {
        var matches = ListItem.Matches(body);
        var items = new List<string>(matches.Count);
        foreach (Match m in matches)
        {
            var text = m.Groups[2].Value.Trim();
            if (text.Length is >= 3 and <= 80)
                items.Add(text);
        }
        // Devolvemos a lista crua. Truncar aqui mascara listas longas que
        // o Generate deveria rejeitar (regra "3-6 itens"). Cloze e
        // Definition cobrem conteúdo com listas grandes.
        return items;
    }

    /// <summary>3 permutações determinísticas, todas ≠ da original. Não
    /// usa Random — o "embaralhamento" é uma rotação pelos primeiros
    /// 3 índices (suficiente para serem diferentes entre si e da
    /// identidade, sem repetir).</summary>
    private static List<List<string>> ThreeDeterministicShuffles(List<string> seq)
    {
        var result = new List<List<string>>();
        if (seq.Count < 2) return result;

        // P1: reverso
        var rev = seq.AsEnumerable().Reverse().ToList();
        if (!rev.SequenceEqual(seq)) result.Add(rev);

        // P2: rotaciona 1 para a esquerda
        var rot = seq.Skip(1).Concat(new[] { seq[0] }).ToList();
        if (!rot.SequenceEqual(seq) && !result.Any(r => r.SequenceEqual(rot))) result.Add(rot);

        // P3: swap dos dois primeiros
        if (seq.Count >= 2)
        {
            var swap = new List<string>(seq) { [0] = seq[1], [1] = seq[0] };
            if (!swap.SequenceEqual(seq) && !result.Any(r => r.SequenceEqual(swap))) result.Add(swap);
        }

        // P4 (fallback): swap do meio se houver
        if (result.Count < 3 && seq.Count >= 4)
        {
            var mid = new List<string>(seq);
            (mid[1], mid[2]) = (mid[2], mid[1]);
            if (!mid.SequenceEqual(seq) && !result.Any(r => r.SequenceEqual(mid))) result.Add(mid);
        }

        return result;
    }

    private static string FormatSequence(IEnumerable<string> items) =>
        string.Join(" → ", items);
}
