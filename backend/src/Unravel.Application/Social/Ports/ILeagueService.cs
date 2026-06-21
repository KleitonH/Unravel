namespace Unravel.Application.Social.Ports;

/// <summary>PR 66 — ligas semanais.</summary>

public record LeagueMemberDto(
    Guid    UserId,
    string  Name,
    int     WeeklyXp,
    int     Rank,
    bool    IsMine);

public record MyLeagueDto(
    string  Tier,
    string? NextTier,        // pra onde sobe (null se Mestre)
    string? PrevTier,        // pra onde desce (null se Bronze)
    int     WeeklyXp,
    int     Rank,
    int     Size,
    int     PromoteZone,     // nº de posições do topo que sobem
    int     RelegateZone,    // nº de posições do fim que descem
    string? LastResult,      // desfecho da semana anterior: promoted|relegated|stayed
    int?    LastRank,
    string  WeekEndsAt,
    IReadOnlyList<LeagueMemberDto> Leaderboard);

public interface ILeagueService
{
    /// <summary>Liga do aluno na semana corrente (faz o rollover se virou a semana).</summary>
    Task<MyLeagueDto> GetMyLeagueAsync(Guid userId, DateTime now, CancellationToken ct = default);
}
