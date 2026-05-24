using Unravel.Domain.Entities;
using Unravel.Domain.Gamification;
using Unravel.Domain.ValueObjects;

namespace Unravel.Tests.Gamification;

public class StreakUpdaterTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 14, 30, 0, DateTimeKind.Utc);

    private static User NewUser() => User.Create("Test", Email.Create("t@x.com"), "hash");

    [Fact]
    public void FirstActivity_SetsStreakToOne()
    {
        var u = NewUser();
        Assert.Null(u.LastActivityDate);
        Assert.Equal(0, u.StreakDays);

        StreakUpdater.RegisterActivity(u, Now);
        Assert.Equal(1, u.StreakDays);
        Assert.Equal(Now, u.LastActivityDate);
    }

    [Fact]
    public void ActivityYesterday_IncrementsStreak_AndUpdatesLongest()
    {
        var u = NewUser();
        u.StreakDays = 5;
        u.LongestStreak = 5;
        u.LastActivityDate = Now.AddDays(-1);

        StreakUpdater.RegisterActivity(u, Now);
        Assert.Equal(6, u.StreakDays);
        Assert.Equal(6, u.LongestStreak);
    }

    [Fact]
    public void ActivityYesterday_DoesNotOverwriteLongestIfSmaller()
    {
        var u = NewUser();
        u.StreakDays = 5;
        u.LongestStreak = 30;
        u.LastActivityDate = Now.AddDays(-1);

        StreakUpdater.RegisterActivity(u, Now);
        Assert.Equal(6, u.StreakDays);
        Assert.Equal(30, u.LongestStreak);  // não regrediu
    }

    [Fact]
    public void ActivityToday_DoesNotIncrement_ButRefreshesTimestamp()
    {
        var u = NewUser();
        u.StreakDays = 3;
        u.LastActivityDate = Now.AddHours(-2);

        StreakUpdater.RegisterActivity(u, Now);
        Assert.Equal(3, u.StreakDays);                // não duplicou
        Assert.Equal(Now, u.LastActivityDate);        // timestamp avança
    }

    [Fact]
    public void GapOfTwoOrMoreDays_ResetsToOne()
    {
        var u = NewUser();
        u.StreakDays = 12;
        u.LastActivityDate = Now.AddDays(-3);

        StreakUpdater.RegisterActivity(u, Now);
        Assert.Equal(1, u.StreakDays);
    }
}
