using Unravel.Application.Forge.Ports;
using Unravel.Application.Knowledge.Ports;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Knowledge;

namespace Unravel.Infrastructure.Forge;

/// <summary>
/// Versão semântica de <see cref="IDistractorPicker"/> usando embeddings
/// MiniLM (PR 18). Substitui o <see cref="DistractorPicker"/> lexical (Jaccard)
/// quando <c>Embedding:Enabled</c> está ligado.
///
/// <para><b>Por que isso é melhor</b>: o picker lexical escolhe "React"
/// como distrator de "Closure" porque ambos aparecem em conteúdos JS — mas
/// um aluno minimamente preparado descarta. O semântico escolhe "Lambda"
/// ou "Function expression" — coisas que de fato confundem.</para>
///
/// <para><b>Pipeline</b>:</para>
/// <list type="number">
///   <item>Encoda o termo correto.</item>
///   <item>Para cada keyword candidata de outros topics, calcula cosine sim.</item>
///   <item>Ordena por similaridade <i>decrescente</i> (mais próximo = mais
///   plausível como distrator), filtrando os "muito próximos" (sim ≥ 0.95
///   — provavelmente sinônimos que enganariam até o gabarito).</item>
///   <item>Retorna top-N. Tie-break por TopicId e termo (determinismo).</item>
/// </list>
/// </summary>
public sealed class SemanticDistractorPicker : IDistractorPicker
{
    private readonly IEmbedder _embedder;

    /// <summary>Acima desse cosine sim, consideramos o candidato sinônimo
    /// virtual — confundiria o gabarito. 0.95 é conservador para vetores
    /// L2-normalizados (sim = dot product); ajustar se necessário.</summary>
    public double NearSynonymThreshold { get; init; } = 0.95;

    public SemanticDistractorPicker(IEmbedder embedder) => _embedder = embedder;

    public IReadOnlyList<string> Pick(
        string correctTerm, Topic sourceTopic, KnowledgeGraph graph, int count)
    {
        if (string.IsNullOrWhiteSpace(correctTerm) || count <= 0)
            return Array.Empty<string>();

        var correctKey  = TextNormalizer.CanonicalKey(correctTerm);
        var correctVec  = _embedder.Encode(correctTerm).ToArray(); // ToArray pra capturar em lambda
        var correctArity = correctTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        var candidates = graph.Topics
            .Where(t => t.Id != sourceTopic.Id)
            .SelectMany(t => t.Keywords.Select(k => new
            {
                t.Id,
                Term  = k.Term,
                Key   = string.Join(' ', k.Term
                                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                .Select(TextNormalizer.CanonicalKey)),
                Arity = k.Term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            }))
            .Where(x => !string.Equals(x.Key, correctKey, StringComparison.Ordinal))
            .GroupBy(x => x.Key)                       // dedupe por chave canonical
            .Select(g => g.OrderBy(x => x.Id).First())
            .Select(x =>
            {
                var candVec = _embedder.Encode(x.Term).ToArray();
                var sim = IEmbedder.CosineSimilarity(correctVec, candVec);
                return (x.Term, x.Arity, Similarity: sim);
            })
            .Where(x => x.Similarity < NearSynonymThreshold)
            .OrderByDescending(x => x.Similarity)
            .ThenBy(x => Math.Abs(x.Arity - correctArity))   // preferimos mesma arity (1 palavra vs 1 palavra)
            .ThenBy(x => x.Term, StringComparer.Ordinal)
            .Take(count * 3)
            .Select(x => MatchCase(x.Term, correctTerm))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToList();

        return candidates;
    }

    /// <summary>Aplica padrão de capitalização da resposta correta ao distrator
    /// (mesmo helper do <c>DistractorPicker</c> lexical — mantém ambos
    /// estética-compatíveis).</summary>
    private static string MatchCase(string term, string reference)
    {
        var letters = reference.Where(char.IsLetter).ToList();
        if (letters.Count == 0) return term;
        var upperRatio = (double)letters.Count(char.IsUpper) / letters.Count;
        if (upperRatio >= 0.8) return term.ToUpperInvariant();

        var parts = term.Split(' ');
        for (var i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        return string.Join(' ', parts);
    }
}
