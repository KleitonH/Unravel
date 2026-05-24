using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.Ports;

/// <summary>
/// Extrai keywords ponderadas de um texto. Implementação determinística
/// (mesmo input → mesmo output) é exigida para que o KnowledgeGraph seja
/// reproduzível e cacheável por hash dos Contents.
/// </summary>
public interface IKeywordExtractor
{
    /// <summary>
    /// Retorna até <paramref name="topN"/> keywords ordenadas por score
    /// decrescente. Termos já vêm normalizados (lowercase + stem leve
    /// PT-BR conforme implementação). Score é proporcional à relevância
    /// dentro do texto (RAKE, TF-IDF ou similar).
    /// </summary>
    IReadOnlyList<Keyword> Extract(string text, int topN = 12);
}
