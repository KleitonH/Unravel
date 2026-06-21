using Unravel.Domain.Gamification;

namespace Unravel.Tests.LiveQuiz;

public class LiveQuizScoringTests
{
    [Fact]
    public void Wrong_is_zero()
        => Assert.Equal(0, LiveQuizScoring.Points(correct: false, msToAnswer: 0, limitSeconds: 20));

    [Fact]
    public void Instant_correct_is_max()
        => Assert.Equal(1000, LiveQuizScoring.Points(true, 0, 20));

    [Fact]
    public void Correct_at_limit_is_base()
        => Assert.Equal(500, LiveQuizScoring.Points(true, 20_000, 20));

    [Fact]
    public void Correct_beyond_limit_clamps_to_base()
        => Assert.Equal(500, LiveQuizScoring.Points(true, 999_999, 20));

    [Fact]
    public void Correct_midway_is_between()
        => Assert.Equal(750, LiveQuizScoring.Points(true, 10_000, 20));

    [Fact]
    public void Faster_scores_more()
        => Assert.True(LiveQuizScoring.Points(true, 1_000, 20) > LiveQuizScoring.Points(true, 9_000, 20));
}
