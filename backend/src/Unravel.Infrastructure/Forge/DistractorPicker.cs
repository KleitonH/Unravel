using Unravel.Application.Forge.Ports;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Infrastructure.Forge;

/// <summary>
/// Implementação lexical de <see cref="IDistractorPicker"/>: pega
/// keywords de outros tópicos da trilha que pareçam morfologicamente
/// "do mesmo tipo" da resposta correta (mesma contagem de palavras,
/// case-pattern similar), priorizando tópicos vizinhos no DAG.
///
/// <para><b>Por que isso funciona</b>: em conteúdo técnico, termos de
/// uma mesma trilha pertencem geralmente ao mesmo domínio conceitual.
/// Se a resposta correta é "Hexagonal", "MVC" e "Layered" (tirados de
/// outros Contents) são distratores muito mais plausíveis que "Banana".</para>
///
/// <para><b>Limites conhecidos</b>: sem embeddings, não capturamos
/// "Lambda" como mais próximo de "Closure" que "Variable". Quando
/// BERTimbau entrar, esse picker pode ser substituído por uma versão
/// semantic-first; o resto do Forge não muda.</para>
/// </summary>
public sealed class DistractorPicker : IDistractorPicker
{
    public IReadOnlyList<string> Pick(string correctTerm, Topic sourceTopic,
                                      KnowledgeGraph graph, int count)
    {
        if (string.IsNullOrWhiteSpace(correctTerm) || count <= 0)
            return Array.Empty<string>();

        var correctKey  = TextNormalizer.CanonicalKey(correctTerm);
        var correctArity = correctTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // Candidatos: keywords de QUALQUER topic ≠ sourceTopic, exceto o termo
        // correto. Priorização aninhada (em ordem): vizinhos diretos no DAG,
        // depois mesma trilha. Tie-break pela diferença de arity (mesmo
        // tamanho composto = mais plausível).
        var neighborIds = graph.GetPrerequisitesOf(sourceTopic.Id)
                               .Select(e => e.FromTopicId)
                               .Union(graph.GetUnlockedBy(sourceTopic.Id).Select(e => e.ToTopicId))
                               .ToHashSet();

        var candidates = graph.Topics
            .Where(t => t.Id != sourceTopic.Id)
            .SelectMany(t => t.Keywords.Select(k => new
            {
                t.Id,
                Term = k.Term,
                Key  = string.Join(' ', k.Term.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                              .Select(TextNormalizer.CanonicalKey)),
                k.Score,
                IsNeighbor = neighborIds.Contains(t.Id),
                Arity = k.Term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            }))
            .Where(x => !string.Equals(x.Key, correctKey, StringComparison.Ordinal))
            .GroupBy(x => x.Key)                       // 1 entrada por chave canonical
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.IsNeighbor)
            .ThenBy(x => Math.Abs(x.Arity - correctArity))
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.Term, StringComparer.Ordinal) // tie-break determinístico
            .Take(count * 3)                            // pega folga; QualityGate filtra duplicatas
            .Select(x => MatchCase(x.Term, correctTerm))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToList();

        return candidates;
    }

    /// <summary>Aplica o padrão de capitalização da resposta correta ao
    /// distrator. "hexagonal" + correto "MVC" → "Hexagonal" (Title); "MVC" →
    /// "HEXAGONAL". Heurística: se >80% das letras do correto são uppercase,
    /// uppercase tudo; senão, capitaliza a primeira de cada palavra.</summary>
    private static string MatchCase(string term, string reference)
    {
        var letters = reference.Where(char.IsLetter).ToList();
        if (letters.Count == 0) return term;
        var upperRatio = (double)letters.Count(char.IsUpper) / letters.Count;

        if (upperRatio >= 0.8) return term.ToUpperInvariant();

        // Title case por palavra.
        var parts = term.Split(' ');
        for (var i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        return string.Join(' ', parts);
    }
}
