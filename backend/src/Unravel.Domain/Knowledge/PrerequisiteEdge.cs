namespace Unravel.Domain.Knowledge;

/// <summary>Aresta dirigida de pré-requisito: dominar <c>FromTopicId</c> é
/// recomendado antes de avançar para <c>ToTopicId</c>. <c>Weight</c> é a
/// similaridade lexical que motivou a aresta (0..1) — preservada para que
/// o planner possa preferir desbloqueios mais "fortes".</summary>
public sealed record PrerequisiteEdge(int FromTopicId, int ToTopicId, double Weight);
