namespace Unravel.Application.Social.Ports;

/// <summary>PR 64 — mecânicas sociais (Amigos/Parcerias).</summary>

/// <summary>Amigo aceito, com stats pro placar entre amigos.</summary>
public record FriendDto(
    Guid    UserId,
    string  Name,
    int     Xp,
    int     StreakDays,
    string? ActiveTitle,
    int     BadgeCount,
    int     FriendshipId);

/// <summary>Pedido de amizade pendente. Direction: "incoming" | "outgoing".</summary>
public record FriendRequestDto(
    int     FriendshipId,
    Guid    UserId,
    string  Name,
    int     Xp,
    string? ActiveTitle,
    string  CreatedAt,
    string  Direction);

public record FriendRequestsDto(
    IReadOnlyList<FriendRequestDto> Incoming,
    IReadOnlyList<FriendRequestDto> Outgoing);

/// <summary>
/// Resultado de busca de usuário. RelationStatus: "none" | "pending_out"
/// | "pending_in" | "friends" | "blocked".
/// </summary>
public record UserSearchDto(
    Guid    UserId,
    string  Name,
    int     Xp,
    string? ActiveTitle,
    string  RelationStatus);

public enum FriendActionOutcome
{
    Ok,
    NotFound,
    CannotSelf,
    AlreadyFriends,
    AlreadyPending,
    Blocked,
    NotAuthorized,
}

public record FriendActionResult(FriendActionOutcome Outcome, int? FriendshipId = null);

public interface IFriendshipService
{
    /// <summary>Amigos aceitos, ordenados por XP desc (placar entre amigos).</summary>
    Task<IReadOnlyList<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Pedidos pendentes recebidos e enviados.</summary>
    Task<FriendRequestsDto> GetRequestsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Busca usuários (Students ativos) por nome, anotando a relação com o solicitante.</summary>
    Task<IReadOnlyList<UserSearchDto>> SearchAsync(Guid userId, string query, int take, CancellationToken ct = default);

    /// <summary>Envia pedido de amizade.</summary>
    Task<FriendActionResult> SendRequestAsync(Guid requesterId, Guid addresseeId, CancellationToken ct = default);

    /// <summary>Responde um pedido recebido (aceitar ou recusar).</summary>
    Task<FriendActionResult> RespondAsync(Guid userId, int friendshipId, bool accept, CancellationToken ct = default);

    /// <summary>Remove uma amizade existente (qualquer direção).</summary>
    Task<FriendActionResult> RemoveAsync(Guid userId, Guid otherUserId, CancellationToken ct = default);
}
