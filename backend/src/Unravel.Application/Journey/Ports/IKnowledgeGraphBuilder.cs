using Unravel.Domain.Entities;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.Ports;

/// <summary>
/// Constrói um <see cref="KnowledgeGraph"/> a partir dos Contents brutos de uma
/// trilha. Separado do cache porque o builder é puramente computacional (não
/// fala com BD nem mantém estado) — facilita testar com inputs sintéticos.
/// </summary>
public interface IKnowledgeGraphBuilder
{
    KnowledgeGraph Build(int trailId, IReadOnlyList<Content> contents);
}
