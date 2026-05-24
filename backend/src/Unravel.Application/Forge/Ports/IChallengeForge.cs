using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Orquestrador: dado um <see cref="Content"/>, gera um pool de
/// rascunhos rodando todas as estratégias registradas, aplica o
/// QualityGate e devolve os aprovados, calibrados para o nível alvo do
/// usuário.
///
/// <para>Não persiste — quem chama (o use case) decide se aceita os
/// drafts no pool de <c>GeneratedChallenge</c> ou os descarta. Mantém
/// o Forge livre de DB.</para>
/// </summary>
public interface IChallengeForge
{
    /// <summary>
    /// Produz até <paramref name="targetCount"/> drafts validados para
    /// este conteúdo.
    /// </summary>
    /// <param name="targetUserMastery">Mastery efetivo do usuário no
    /// tópico (0..1). O Forge prefere drafts com dificuldade estimada
    /// próxima de <c>targetUserMastery + 0.15</c> (zona proximal).</param>
    IReadOnlyList<GeneratedChallengeDraft> Build(
        Content content,
        KnowledgeGraph graph,
        int targetCount,
        double targetUserMastery = 0.3);
}
