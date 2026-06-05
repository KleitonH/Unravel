using Unravel.Domain.Forge;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// PR 37 — porta de persistência pra <see cref="UserSeenChallenge"/>.
/// Usado por:
///
/// <list type="bullet">
///   <item><b>SubmitPoolChallengeUseCase</b>: marca como visto quando aluno
///   responde uma pergunta gerada (UPSERT idempotente).</item>
///   <item><b>BuildReinforcementQuizUseCase</b>: lista IDs vistos pra
///   anti-join (excluir do pool de reforço).</item>
/// </list>
///
/// <para>Sem método de "delete" — vistos são histórico imutável. Reciclagem
/// futura ("perguntas vistas há 30+ dias podem reaparecer") será feita por
/// filtro de data no use case, não por remoção da linha.</para>
/// </summary>
public interface IUserSeenChallengeRepository
{
    /// <summary>UPSERT — primeira chamada insere; subsequentes atualizam
    /// <c>SeenAt</c> e <c>WasCorrect</c>. Idempotente: chamar 2x na mesma
    /// resposta não duplica linhas.</summary>
    Task MarkAsync(Guid userId, int generatedChallengeId, bool wasCorrect,
                   DateTime seenAt, CancellationToken ct = default);

    /// <summary>IDs de generated_challenge já vistos pelo user dentro do
    /// conjunto candidato. Retorna HashSet pra filtro O(1) no caller.</summary>
    Task<HashSet<int>> GetSeenIdsAsync(Guid userId,
                                       IReadOnlyCollection<int> candidateIds,
                                       CancellationToken ct = default);
}
