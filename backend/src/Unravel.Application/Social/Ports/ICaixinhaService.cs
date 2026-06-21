namespace Unravel.Application.Social.Ports;

/// <summary>PR 65 — Caixinha de Gatos (clã/grupo social).</summary>

public record CaixinhaMemberDto(
    Guid    UserId,
    string  Name,
    int     Xp,
    int     StreakDays,
    bool    ActiveToday,
    string  Role);

public record CaixinhaMessageDto(
    int     Id,
    Guid    UserId,
    string  AuthorName,
    string  Text,
    string  CreatedAt);

/// <summary>Detalhe da caixinha do usuário (painel completo).</summary>
public record CaixinhaDetailDto(
    int     Id,
    string  Name,
    string  Emblem,
    Guid    LeaderId,
    int     CollectivePoints,
    int     MemberCount,
    int     ActiveTodayCount,
    int     Rank,
    string  MyRole,
    int     DailyGoal,           // PR 67 — meta coletiva diária
    int     DailyPoints,         // pontos do dia (0 se virou o dia)
    bool    GoalReachedToday,
    IReadOnlyList<CaixinhaMemberDto>  Members,
    IReadOnlyList<CaixinhaMessageDto> Mural);

/// <summary>Resumo pra listagem/ranking de caixinhas.</summary>
public record CaixinhaSummaryDto(
    int     Id,
    string  Name,
    string  Emblem,
    int     MemberCount,
    int     CollectivePoints,
    int     Rank);

public enum CaixinhaOutcome
{
    Ok,
    AlreadyInOne,
    NotInAny,
    NotFound,
    Full,
    NotLeader,
    NotMember,
    NameTooShort,
    EmptyMessage,
    Disbanded,
}

public record CaixinhaActionResult(CaixinhaOutcome Outcome, int? CaixinhaId = null);

public interface ICaixinhaService
{
    /// <summary>Caixinha do usuário (null se não pertence a nenhuma).</summary>
    Task<CaixinhaDetailDto?> GetMineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Cria uma caixinha; o criador vira líder.</summary>
    Task<CaixinhaActionResult> CreateAsync(Guid userId, string name, string? emblem, CancellationToken ct = default);

    /// <summary>Lista caixinhas pra entrar (busca por nome; vazio = ranking geral).</summary>
    Task<IReadOnlyList<CaixinhaSummaryDto>> BrowseAsync(string? query, int take, CancellationToken ct = default);

    /// <summary>Ranking de caixinhas por pontos coletivos.</summary>
    Task<IReadOnlyList<CaixinhaSummaryDto>> LeaderboardAsync(int top, CancellationToken ct = default);

    /// <summary>Entra numa caixinha (se houver vaga e o usuário não estiver em outra).</summary>
    Task<CaixinhaActionResult> JoinAsync(Guid userId, int caixinhaId, CancellationToken ct = default);

    /// <summary>Sai da caixinha. Líder que sai transfere a liderança (ou dissolve se sozinho).</summary>
    Task<CaixinhaActionResult> LeaveAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Líder remove um membro.</summary>
    Task<CaixinhaActionResult> KickAsync(Guid leaderId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>Posta uma mensagem no mural (apenas membros).</summary>
    Task<CaixinhaActionResult> PostMessageAsync(Guid userId, string text, CancellationToken ct = default);
}
