namespace Unravel.Application.Arena.Ports;

/// <summary>
/// Arena (PvP) — núcleo: matchmaking/desafio direto, ciclo da partida (rodadas
/// com snapshot de questões da trilha-tema), pontuação por acerto+velocidade e
/// ranking. O SignalR empurra o tempo real por cima deste serviço.
/// </summary>

public record ArenaMatchDto(
    int     Id,
    string  Status,
    int     TrailId,
    Guid    Player1Id,
    string  Player1Name,
    Guid?   Player2Id,
    string? Player2Name,
    int     Score1,
    int     Score2,
    Guid?   WinnerId,
    int     CurrentRoundIndex,
    int     TotalRounds,
    int     SecondsPerQuestion,
    IReadOnlyList<ArenaCosmeticDto> Player1Cosmetics,
    IReadOnlyList<ArenaCosmeticDto> Player2Cosmetics);

/// <summary>Cosmético equipado de um jogador (pra montar o NAVI no duelo).</summary>
public record ArenaCosmeticDto(string Slot, string AssetSlug);

/// <summary>Rodada entregue aos jogadores — sem gabarito.</summary>
public record ArenaRoundDto(
    int                   OrderIndex,
    int                   Total,
    string                Prompt,
    IReadOnlyList<string> Options,
    string                Shape,
    int                   SecondsPerQuestion);

public record ArenaRoundResultDto(
    int   OrderIndex,
    int   CorrectIndex,
    int   Score1,
    int   Score2,
    bool  Finished,
    Guid? WinnerId);

public record ArenaRankingRow(int Rank, Guid UserId, string DisplayName, int Points, int Wins, int Losses, int Draws);

public record EnqueueResult(bool Matched, int? MatchId = null);

public enum ArenaActionOutcome { Ok, NotFound, NotAuthorized, CannotSelf, NoQuestions, OpponentNotFound }

public record ArenaActionResult(ArenaActionOutcome Outcome, int? MatchId = null);

public record SubmitArenaResult(
    bool Accepted,
    bool IsCorrect,
    int  Points,
    bool RoundResolved,   // ambos responderam → rodada apurada
    bool MatchFinished,
    int  CorrectIndex);

public interface IArenaService
{
    /// <summary>Entra na fila; pareia com um oponente esperando (mesmo tema) ou fica aguardando.</summary>
    Task<EnqueueResult> EnqueueAsync(Guid userId, int trailId, CancellationToken ct = default);
    Task LeaveQueueAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Desafio direto a outro usuário (gera partida Pending + notificação).</summary>
    Task<ArenaActionResult> ChallengeAsync(Guid challengerId, Guid opponentId, int trailId, CancellationToken ct = default);
    Task<ArenaActionResult> RespondChallengeAsync(int matchId, Guid userId, bool accept, CancellationToken ct = default);

    Task<ArenaMatchDto?> GetMatchAsync(int matchId, CancellationToken ct = default);
    Task<ArenaRoundDto?> CurrentRoundAsync(int matchId, CancellationToken ct = default);
    Task<ArenaRoundResultDto?> RoundResultAsync(int matchId, int orderIndex, CancellationToken ct = default);

    Task<SubmitArenaResult> SubmitAnswerAsync(
        int matchId, Guid userId, int roundIndex, int selectedIndex, DateTime now, CancellationToken ct = default);

    Task<IReadOnlyList<ArenaRankingRow>> RankingAsync(int top, CancellationToken ct = default);

    /// <summary>Partidas ativas + desafios pendentes do usuário (pra UI/notificação).</summary>
    Task<IReadOnlyList<ArenaMatchDto>> MyOpenMatchesAsync(Guid userId, CancellationToken ct = default);
}
