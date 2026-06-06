namespace Unravel.Application.Journey.Ports;

/// <summary>
/// PR 40 — orquestrador da progressão tipo Super Mario World.
/// Chamado pelo hook do <c>SubmitPoolChallengeUseCase</c> a cada submit
/// de pergunta, e pelo endpoint <c>GET /api/trails/{trailId}/map</c>
/// pra montar o estado completo do mapa pro usuário.
///
/// <para><b>Por que service e não use case</b>: a regra é compartilhada
/// (submit + map endpoint), encapsula transição de estado
/// (Available→InProgress→Completed) e gera efeito colateral (unlock do
/// próximo). Lógica densa o suficiente pra justificar service dedicado.</para>
/// </summary>
public interface ITrailProgressService
{
    /// <summary>
    /// Registra que o aluno respondeu uma pergunta de um content específico.
    /// Incrementa <c>ChallengesCompleted</c>, transiciona Status se necessário,
    /// e se atingiu meta: marca como Completed + desbloqueia o próximo
    /// content na ordem da trilha (criando UserContent com Available).
    ///
    /// <para>Idempotente em relação ao Completed — submits após meta atingida
    /// continuam incrementando contagem (histórico) mas não voltam status
    /// nem re-disparam unlock.</para>
    ///
    /// <para>Retorna o estado pós-atualização pra UI exibir feedback
    /// imediato (ex: "🏝️ Ilha concluída!").</para>
    /// </summary>
    Task<ProgressUpdate> RecordChallengeAsync(
        Guid userId, int contentId, CancellationToken ct = default);

    /// <summary>
    /// Estado completo do mapa pro usuário numa trilha — lista de contents
    /// na ordem, com Status calculado pra cada um. Contents sem UserContent
    /// persistido vêm como <c>Locked</c>.
    /// </summary>
    Task<TrailMap?> GetTrailMapAsync(
        Guid userId, int trailId, CancellationToken ct = default);

    /// <summary>
    /// Garante que o aluno tem acesso ao 1º content da trilha. Chamado
    /// no enroll (UserTrail criado) e pela migration de seed pra users
    /// já inscritos. Idempotente — se já existe UserContent pro 1º
    /// content, não faz nada.
    /// </summary>
    Task BootstrapAccessAsync(Guid userId, int trailId, CancellationToken ct = default);

    /// <summary>
    /// PR 41 — relatório de mastery por tópico da trilha pro aluno
    /// (radar de fraquezas). Inclui effective score com decay, confidence,
    /// SRS state e classificação por severity. Topics sem mastery ainda
    /// aparecem (score=0) — útil pra mostrar "ainda não toquei aqui".
    /// </summary>
    Task<TrailMasteryReport?> GetTrailMasteryAsync(
        Guid userId, int trailId, CancellationToken ct = default);
}

/// <summary>Snapshot retornado após RecordChallengeAsync.</summary>
public sealed record ProgressUpdate(
    int  ContentId,
    int  ChallengesCompleted,
    int  ChallengesRequired,
    bool JustCompleted,        // true se ESSA chamada disparou o flip pra Completed
    int? NextContentIdUnlocked // null se já era completed ou não há próximo
);

public sealed record TrailMap(
    int                          TrailId,
    string                       TrailName,
    IReadOnlyList<TrailMapNode>  Nodes
);

/// <summary>PR 41 — radar de fraquezas. Cada item é um tópico da trilha
/// com o score efetivo do user no momento da request, + dados pra UI
/// rankear por severity (Weak / Stale / Solid).</summary>
public sealed record TrailMasteryReport(
    int                              TrailId,
    string                           TrailName,
    double                           AverageEffectiveScore,
    int                              WeakCount,         // effective < 0.6
    int                              SrsDueCount,       // NextDueAt <= now
    int                              UntouchedCount,    // sem mastery registrado
    IReadOnlyList<TopicMasteryItem>  Topics
);

public sealed record TopicMasteryItem(
    int       TopicId,
    string    TopicSlug,
    int       ContentId,
    string    ContentTitle,
    int       Order,
    bool      HasMastery,
    double    RawScore,            // score "fresco" no LastSeenAt
    double    EffectiveScore,      // com decay até now
    int       Confidence,          // n tentativas
    DateTime? LastSeenAt,
    DateTime? NextDueAt,
    bool      IsSrsDue,
    string    Severity             // "Weak" | "Stale" | "Solid"
);

public sealed record TrailMapNode(
    int     ContentId,
    string  Title,
    string? Slug,
    int     Order,
    int     ChallengesRequired,
    int     ChallengesCompleted,
    string  Status,             // "Locked" | "Available" | "InProgress" | "Completed"
    bool    IsRecommended = false  // PR 42b — true se o JourneyPlanner sugeriu pra HOJE
);
