namespace Unravel.Application.Services;

public record ChallengeResponse(
    int    Id,
    int    TrailId,
    string Title,
    string Description,
    string Level,
    int    XpReward,
    int    CoinsReward,
    bool   IsDailyChallenge,
    IEnumerable<OptionResponse> Options
);

public record OptionResponse(int Id, string Text, int Order);

public record SubmitChallengeRequest(
    int                    ChallengeId,
    IEnumerable<AnswerDto> Answers,
    int                    AvgResponseTimeMs
);

public record AnswerDto(int SelectedOptionId);

public record ChallengeResultResponse(
    int     XpEarned,
    int     CoinsEarned,
    int     CorrectAnswers,
    int     TotalQuestions,
    bool    IsPerfect,
    int     NewStreakDays,
    int     TotalXp,
    int     TotalCoins,
    string? BadgeEarned,
    IEnumerable<AnswerResultDto> Results
);

public record AnswerResultDto(int OptionId, bool IsCorrect, string CorrectOptionText);

public record DailyStatusResponse(
    bool               HasDailyChallenge,
    ChallengeResponse? DailyChallenge,
    bool               AlreadyCompletedToday,
    int                StreakDays,
    int                LoginCycleDay,
    string             DailyLoginReward
);

public record CreateChallengeRequest(
    int                          TrailId,
    string                       Title,
    string                       Description,
    int                          Level,
    int                          XpReward,
    int                          CoinsReward,
    bool                         IsDailyChallenge,
    IEnumerable<CreateOptionDto> Options
);

public record CreateOptionDto(string Text, bool IsCorrect, int Order);

public interface IChallengeService
{
    Task<IEnumerable<ChallengeResponse>> GetByTrailAsync(int trailId);
    Task<ChallengeResponse?>             GetByIdAsync(int id);
    Task<DailyStatusResponse>            GetDailyStatusAsync(Guid userId);
    Task<ChallengeResultResponse>        SubmitAsync(Guid userId, SubmitChallengeRequest dto);
    Task<ChallengeResponse>              CreateAsync(CreateChallengeRequest dto);
    Task<bool>                           DeleteAsync(int id);
}
