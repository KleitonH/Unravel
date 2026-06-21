using Unravel.Domain.Entities;

namespace Unravel.Application.LiveQuiz.Ports;

/// <summary>
/// Quiz ao Vivo (estilo Kahoot) — núcleo de sessão. Cria a sessão a partir
/// das perguntas escolhidas (snapshot), gerencia o estado (lobby → rodando →
/// fim), entrada (whitelist de turma ou código livre), respostas e ranking.
/// O SignalR empurra o tempo real por cima deste serviço.
/// </summary>

public record CreateLiveQuizRequest(
    LiveQuizMode        Mode,
    int                 SecondsPerQuestion,
    bool                ShowRankBetween,
    bool                ShuffleQuestions,
    bool                ShuffleOptions,
    IReadOnlyList<int>  QuestionChallengeIds,
    IReadOnlyList<Guid> AllowedUserIds);

public record LiveQuizSessionDto(
    int    Id,
    string JoinCode,
    string Mode,
    string Status,
    int    QuestionCount,
    int    SecondsPerQuestion,
    bool   ShowRankBetween,
    int    ParticipantCount,
    int    CurrentQuestionIndex);

/// <summary>Pergunta entregue aos participantes DURANTE a rodada — sem gabarito.</summary>
public record LiveQuizQuestionDto(
    int                   OrderIndex,
    int                   Total,
    string                Prompt,
    IReadOnlyList<string> Options,
    string                Shape,
    int                   SecondsPerQuestion);

/// <summary>Gabarito da pergunta — revelado ao fim da rodada.</summary>
public record LiveQuizQuestionResultDto(int OrderIndex, int CorrectIndex, string? Explanation);

public record LiveQuizParticipantDto(int Id, Guid UserId, string DisplayName, int Score);

public record LiveQuizLeaderboardRow(int Rank, Guid UserId, string DisplayName, int Score);

public enum JoinOutcome { Ok, NotFound, NotAllowed, Finished }

public record JoinResult(JoinOutcome Outcome, LiveQuizParticipantDto? Participant = null, LiveQuizSessionDto? Session = null);

public record SubmitLiveAnswerResult(
    bool Accepted,      // false = duplicada/fora de hora
    bool IsCorrect,
    int  Points,
    int  TotalScore,
    int  CorrectIndex);

public interface ILiveQuizService
{
    Task<LiveQuizSessionDto> CreateAsync(Guid hostUserId, CreateLiveQuizRequest req, CancellationToken ct = default);
    Task<LiveQuizSessionDto?> GetAsync(int sessionId, CancellationToken ct = default);
    Task<LiveQuizSessionDto?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<JoinResult> JoinAsync(string code, Guid userId, string displayName, CancellationToken ct = default);

    /// <summary>Lobby → Running (host only). Retorna false se não autorizado/estado inválido.</summary>
    Task<bool> StartAsync(int sessionId, Guid hostUserId, CancellationToken ct = default);

    /// <summary>Avança pra próxima pergunta (host only). Retorna o novo índice, ou -1 se encerrou.</summary>
    Task<int> AdvanceAsync(int sessionId, Guid hostUserId, CancellationToken ct = default);

    Task FinishAsync(int sessionId, Guid hostUserId, CancellationToken ct = default);

    /// <summary>Pergunta atual (sem gabarito) — null se não está rodando.</summary>
    Task<LiveQuizQuestionDto?> CurrentQuestionAsync(int sessionId, CancellationToken ct = default);

    /// <summary>Gabarito de uma pergunta da sessão.</summary>
    Task<LiveQuizQuestionResultDto?> QuestionResultAsync(int sessionId, int orderIndex, CancellationToken ct = default);

    Task<SubmitLiveAnswerResult> SubmitAnswerAsync(
        int sessionId, Guid userId, int questionOrderIndex, int selectedIndex, DateTime now, CancellationToken ct = default);

    Task<IReadOnlyList<LiveQuizLeaderboardRow>> LeaderboardAsync(int sessionId, CancellationToken ct = default);

    /// <summary>Quantos participantes já responderam a pergunta de índice dado.</summary>
    Task<int> AnsweredCountAsync(int sessionId, int orderIndex, CancellationToken ct = default);
}
