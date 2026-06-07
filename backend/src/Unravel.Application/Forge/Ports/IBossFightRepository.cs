using Unravel.Domain.Forge;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// PR 50 — port pra operações do Boss Fight que tocam o banco direto.
/// Mantém os use cases (StartBossFightUseCase, SubmitBossFightUseCase)
/// limpos de <c>ApplicationDbContext</c>, respeitando a arquitetura
/// hexagonal.
/// </summary>
public interface IBossFightRepository
{
    /// <summary>Metadados básicos da trilha. Null se não existe ou inativa.</summary>
    Task<BossFightTrailMeta?> GetTrailMetaAsync(int trailId, CancellationToken ct = default);

    /// <summary>Quantos contents regulares ainda faltam pro aluno completar
    /// na trilha. Zero = trilha pronta pro Boss Fight.</summary>
    Task<int> GetIncompleteContentsCountAsync(
        Guid userId, int trailId, CancellationToken ct = default);

    /// <summary>Pool ativo de generated_challenge da trilha. Boss Fight
    /// olha trilha inteira (cruza topics), por isso não filtramos por
    /// content único.</summary>
    Task<IReadOnlyList<GeneratedChallenge>> GetTrailPoolAsync(
        int trailId, CancellationToken ct = default);

    /// <summary>Estado atual do Boss Fight do usuário na trilha. Null se
    /// nunca tentou.</summary>
    Task<UserBossFight?> GetUserBossFightAsync(
        Guid userId, int trailId, CancellationToken ct = default);

    /// <summary>UPSERT do estado. Idempotente — chamar 2x com mesmo
    /// record não duplica linha.</summary>
    Task UpsertUserBossFightAsync(UserBossFight record, CancellationToken ct = default);
}

public sealed record BossFightTrailMeta(int Id, string Name);
