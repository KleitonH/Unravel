using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.Ports;

/// <summary>
/// Cache thread-safe de <see cref="KnowledgeGraph"/> por trilha. Construir o
/// grafo é caro (extração NLP + similaridade O(n²) entre tópicos); para uma
/// trilha que muda raramente, fazer 1x e reutilizar é o padrão correto.
///
/// <para>Invalidação: chamada pelo handler de mudança em Content (criar,
/// editar, deletar). Próximo <see cref="GetOrBuildAsync"/> reconstrói.</para>
/// </summary>
public interface IKnowledgeGraphCache
{
    /// <summary>Retorna o grafo da trilha, construindo-o se ausente. Múltiplas
    /// chamadas concorrentes para o mesmo trailId devem coalescer em uma
    /// única construção.</summary>
    Task<KnowledgeGraph> GetOrBuildAsync(int trailId, CancellationToken ct = default);

    /// <summary>Invalida o cache da trilha. Não força reconstrução imediata —
    /// próxima leitura constrói. Idempotente.</summary>
    void Invalidate(int trailId);
}
