namespace Unravel.Domain.Entities;

public partial class User
{
    // Economia
    public int Xp    { get; set; } = 0;
    public int Coins { get; set; } = 0;
    public int Stars { get; set; } = 0;
    public int Lives { get; set; } = 5;

    // Streak (Ideia 2)
    public int       StreakDays        { get; set; } = 0;
    public int       LongestStreak     { get; set; } = 0;
    public DateTime? LastActivityDate  { get; set; }
    public DateTime? LastLoginDate     { get; set; }
    public int       LoginCycleDay     { get; set; } = 0;

    // Identidade social (Ideias 3 e 5)
    public string? ActiveTitle       { get; set; }
    public int?    ActiveCosmeticId  { get; set; }

    // Navegação
    public ICollection<UserChallenge> ChallengeAttempts { get; set; } = new List<UserChallenge>();
    public ICollection<UserBadge>     Badges            { get; set; } = new List<UserBadge>();
    public ICollection<UserCosmetic>  Cosmetics         { get; set; } = new List<UserCosmetic>();
}
