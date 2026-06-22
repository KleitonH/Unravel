namespace Unravel.Application.Social.Ports;

/// <summary>Resultado de uma ação sobre parceria (convidar/responder/desfazer).</summary>
public enum PartnershipOutcome
{
    Ok,
    NotFound,
    CannotSelf,
    NotFriends,    // a mecânica exige amizade prévia (Ideia 1, pré-condição)
    AlreadyExists, // já há parceria pendente/ativa com esse usuário
    LimitReached,  // teto de parcerias ativas atingido (3)
    NotAuthorized, // não é o destinatário / não participa da parceria
}

public record PartnershipActionResult(PartnershipOutcome Outcome, int PartnershipId = 0);

/// <summary>Estado do novelo atual de uma parceria, do ponto de vista do usuário.</summary>
public record YarnBallDto(
    int    Id,
    Guid   OwnerId,
    bool   IsMyTurn,        // o novelo está comigo agora?
    int    Progress,
    int    DailyGoal,
    int    CyclesCompleted,
    int    TotalCycles,
    string State);          // "Ok" | "Tangled" | "Dropped"

public record PartnershipDto(
    int        Id,
    Guid       PartnerId,
    string     PartnerName,
    int        PartnerXp,
    string?    PartnerTitle,
    string     Status,            // PartnershipStatus
    int        NovelosCompleted,
    YarnBallDto? Yarn);

public record PartnershipRequestDto(
    int    Id,
    Guid   PartnerId,
    string PartnerName,
    int    PartnerXp,
    string Direction,   // "incoming" | "outgoing"
    string CreatedAt);  // dd/MM/yyyy

public record PartnershipRequestsDto(
    IReadOnlyList<PartnershipRequestDto> Incoming,
    IReadOnlyList<PartnershipRequestDto> Outgoing);

/// <summary>
/// Retorno do write-path <see cref="IPartnershipService.AddProgressAsync"/>:
/// quantos novelos foram tocados, e se houve passagem / conclusão.
/// </summary>
public record AddProgressResult(int Updated, bool Passed, bool NoveloCompleted);

/// <summary>
/// Mecânica de Parceria (Ideia 1 / UC17). Convite → aceite → ciclo do novelo
/// (cumpre meta → passa pro parceiro) → conclusão com recompensa. Falha de
/// inatividade enrola/derruba o novelo via <see cref="EvaluateInactivityAsync"/>.
/// </summary>
public interface IPartnershipService
{
    Task<PartnershipActionResult> RequestAsync(Guid requesterId, Guid addresseeId, CancellationToken ct = default);
    Task<PartnershipActionResult> RespondAsync(Guid userId, int partnershipId, bool accept, CancellationToken ct = default);
    Task<PartnershipActionResult> BreakAsync(Guid userId, int partnershipId, CancellationToken ct = default);

    Task<IReadOnlyList<PartnershipDto>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<PartnershipRequestsDto>        GetRequestsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Write-path: o usuário cumpriu tarefas; desenrola o novelo de
    /// toda parceria ativa em que ele é o dono atual. Idempotente por chamada.</summary>
    Task<AddProgressResult> AddProgressAsync(Guid userId, int amount, CancellationToken ct = default);

    /// <summary>Cron: marca novelos parados como enrolados (1 dia) / caídos (2+
    /// dias). Retorna quantos novelos foram afetados.</summary>
    Task<int> EvaluateInactivityAsync(DateTime now, CancellationToken ct = default);
}
