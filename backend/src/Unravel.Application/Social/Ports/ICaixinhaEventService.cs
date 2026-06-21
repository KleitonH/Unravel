namespace Unravel.Application.Social.Ports;

/// <summary>PR 65c — eventos entre caixinhas (competições temporárias).</summary>

public record CaixinhaEventDto(
    int     Id,
    string  Name,
    string? Theme,
    string  StartsAt,
    string  EndsAt,
    string  Status,           // "upcoming" | "active" | "finished"
    int     ParticipantCount,
    bool    MyCaixinhaJoined);

public record EventRankingEntryDto(
    int     CaixinhaId,
    string  Name,
    string  Emblem,
    int     Points,
    int     Rank,
    bool    IsMine);

public record CaixinhaEventDetailDto(
    CaixinhaEventDto Event,
    IReadOnlyList<EventRankingEntryDto> Ranking);

public enum CaixinhaEventOutcome
{
    Ok,
    NotFound,
    InvalidDates,
    NameTooShort,
    NotInAny,
    NotLeader,
    NotActive,
    AlreadyJoined,
}

public record CaixinhaEventResult(CaixinhaEventOutcome Outcome, int? EventId = null);

public interface ICaixinhaEventService
{
    /// <summary>Cria um evento (uso do moderador/plataforma).</summary>
    Task<CaixinhaEventResult> CreateAsync(Guid creatorUserId, string name, string? theme, DateTime startsAt, DateTime endsAt, CancellationToken ct = default);

    /// <summary>Lista eventos (status derivado de `now`), ativos primeiro.</summary>
    Task<IReadOnlyList<CaixinhaEventDto>> ListAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>Detalhe + ranking ao vivo de um evento (congela ao encerrar).</summary>
    Task<CaixinhaEventDetailDto?> GetDetailAsync(Guid userId, int eventId, DateTime now, CancellationToken ct = default);

    /// <summary>O líder inscreve a caixinha num evento ativo (snapshot do baseline).</summary>
    Task<CaixinhaEventResult> JoinAsync(Guid userId, int eventId, DateTime now, CancellationToken ct = default);
}
