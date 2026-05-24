namespace Unravel.Application.Journey.Ports;

/// <summary>
/// Leituras agregadas que o <c>GetDailyJourneyUseCase</c> precisa do banco
/// e que não cabem em <see cref="IMasteryRepository"/> nem no
/// <see cref="IKnowledgeGraphCache"/>. Mantém a Application livre de
/// referência direta a <c>ApplicationDbContext</c>, preservando a
/// arquitetura hexagonal.
///
/// <para>Operações são propositalmente granulares ao caso de uso, não
/// genéricas — port de leitura é melhor quando reflete intenção do
/// consumidor, não a forma da tabela.</para>
/// </summary>
public interface IJourneyReadModel
{
    /// <summary>Metadados básicos da trilha. <c>null</c> se a trilha não
    /// existe ou está inativa.</summary>
    Task<TrailMeta?> GetTrailMetaAsync(int trailId, CancellationToken ct = default);

    /// <summary>Estado do usuário relevante para o planner (vidas, streak).
    /// <c>null</c> se o usuário não existe.</summary>
    Task<UserJourneyState?> GetUserStateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Títulos dos Contents referenciados pelo plano, em um único
    /// round-trip. Chaves ausentes na tabela retornada não geram erro —
    /// o use case faz fallback para "(sem título)".</summary>
    Task<IReadOnlyDictionary<int, string>> GetContentTitlesAsync(
        IReadOnlyCollection<int> contentIds, CancellationToken ct = default);
}

public sealed record TrailMeta(int Id, string Name);
public sealed record UserJourneyState(int Lives, int StreakDays);
