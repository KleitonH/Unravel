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

    /// <summary>PR 61 — quantos desafios o usuário <i>respondeu</i> hoje nesta
    /// trilha (janela [from, to)). Conta <c>UserSeenChallenge</c> (gravado no
    /// submit do pool) cruzado com <c>GeneratedChallenge.TrailId</c> — reflete
    /// o estudo real, não o fluxo antigo de <c>UserChallenge</c>.</summary>
    Task<int> CountChallengesAnsweredAsync(
        Guid userId, int trailId, DateTime fromInclusiveUtc, DateTime toExclusiveUtc,
        CancellationToken ct = default);

    /// <summary>PR 61 — meta efetiva de hoje + penalidade aplicada, lidas do
    /// snapshot do cron (que já inclui o +1). <c>null</c> se ainda não há
    /// snapshot para hoje (ex.: antes do 1º cron) — o use case então cai na
    /// meta-base do planner.</summary>
    Task<TodayGoal?> GetTodayGoalAsync(
        Guid userId, int trailId, DateTime today, CancellationToken ct = default);
}

public sealed record TrailMeta(int Id, string Name);
public sealed record UserJourneyState(int Lives, int StreakDays);
public sealed record TodayGoal(int MetaDia, int Penalty);
