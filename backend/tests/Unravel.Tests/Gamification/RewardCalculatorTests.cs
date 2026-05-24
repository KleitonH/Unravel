using Unravel.Domain.Gamification;

namespace Unravel.Tests.Gamification;

public class RewardCalculatorTests
{
    [Fact]
    public void Compute_Wrong_NoXpOrCoins_LosesLife()
    {
        var r = RewardCalculator.Compute(difficulty: 0.5, correct: false);
        Assert.Equal(0, r.Xp);
        Assert.Equal(0, r.Coins);
        Assert.Equal(0, r.Stars);
        Assert.Equal(-1, r.LifeDelta);
    }

    [Fact]
    public void Compute_CorrectAtMinDifficulty_GivesBaseXpAndCoins_NoStarsNoLifeLoss()
    {
        var r = RewardCalculator.Compute(difficulty: 0, correct: true);
        Assert.Equal(50, r.Xp);
        Assert.Equal(5, r.Coins);
        Assert.Equal(0, r.Stars);       // abaixo do threshold de difficulty
        Assert.Equal(0, r.LifeDelta);
    }

    [Fact]
    public void Compute_CorrectAtMaxDifficulty_MaxXp150_MaxCoins10_OneStar()
    {
        var r = RewardCalculator.Compute(difficulty: 1, correct: true);
        Assert.Equal(150, r.Xp);
        Assert.Equal(10, r.Coins);
        Assert.Equal(1, r.Stars);
        Assert.Equal(0, r.LifeDelta);
    }

    [Theory]
    [InlineData(0.5, 1)]   // exatamente no threshold ⇒ ganha estrela
    [InlineData(0.49, 0)]  // logo abaixo ⇒ nada
    [InlineData(0.7, 1)]
    public void Compute_StarsRespectDifficultyThreshold(double diff, int expectedStars)
    {
        var r = RewardCalculator.Compute(diff, correct: true);
        Assert.Equal(expectedStars, r.Stars);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Compute_RejectsOutOfRangeDifficulty(double diff)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RewardCalculator.Compute(diff, true));
    }

    [Fact]
    public void Compute_IsDeterministic()
    {
        var a = RewardCalculator.Compute(0.42, true);
        var b = RewardCalculator.Compute(0.42, true);
        Assert.Equal(a, b);
    }
}
