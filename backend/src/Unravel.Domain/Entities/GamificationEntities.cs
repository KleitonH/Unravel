namespace Unravel.Domain.Entities;

public class Challenge
{
    public int    Id               { get; set; }
    public int    TrailId          { get; set; }
    public Trail  Trail            { get; set; } = null!;
    public string Title            { get; set; } = string.Empty;
    public string Description      { get; set; } = string.Empty;
    public ChallengeType   Type    { get; set; } = ChallengeType.MultipleChoice;
    public DifficultyLevel Level   { get; set; } = DifficultyLevel.Beginner;
    public int  XpReward           { get; set; } = 150;
    public int  CoinsReward        { get; set; } = 10;
    public bool IsActive           { get; set; } = true;
    public bool IsDailyChallenge   { get; set; } = false;
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;

    public ICollection<ChallengeOption> Options      { get; set; } = new List<ChallengeOption>();
    public ICollection<UserChallenge>   UserAttempts { get; set; } = new List<UserChallenge>();
}

public enum ChallengeType
{
    MultipleChoice = 1,
    OpenAnswer     = 2
}

public class ChallengeOption
{
    public int       Id          { get; set; }
    public int       ChallengeId { get; set; }
    public Challenge Challenge   { get; set; } = null!;
    public string    Text        { get; set; } = string.Empty;
    public bool      IsCorrect   { get; set; } = false;
    public int       Order       { get; set; } = 0;
}

public class UserChallenge
{
    public int       Id                { get; set; }
    public Guid      UserId            { get; set; }
    public User      User              { get; set; } = null!;
    public int       ChallengeId       { get; set; }
    public Challenge Challenge         { get; set; } = null!;
    public int       CorrectAnswers    { get; set; } = 0;
    public int       TotalQuestions    { get; set; } = 0;
    public int       XpEarned          { get; set; } = 0;
    public int       CoinsEarned       { get; set; } = 0;
    public bool      IsCompleted       { get; set; } = false;
    public bool      IsPerfect         { get; set; } = false;
    public int       AvgResponseTimeMs { get; set; } = 0;
    public DateTime  StartedAt         { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt       { get; set; }
}

public class Badge
{
    public int           Id          { get; set; }
    public string        Name        { get; set; } = string.Empty;
    public string        Description { get; set; } = string.Empty;
    public string        Icon        { get; set; } = string.Empty;
    public BadgeCategory Category    { get; set; } = BadgeCategory.Achievement;
    public bool          IsExclusive { get; set; } = false;
    public DateTime      CreatedAt   { get; set; } = DateTime.UtcNow;

    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}

public enum BadgeCategory
{
    Streak      = 1,
    Speed       = 2,
    Knowledge   = 3,
    Arena       = 4,
    Social      = 5,
    Guild       = 6,
    Event       = 7,
    PreRegister = 8,
    Achievement = 9
}

public class UserBadge
{
    public int      Id       { get; set; }
    public Guid     UserId   { get; set; }
    public User     User     { get; set; } = null!;
    public int      BadgeId  { get; set; }
    public Badge    Badge    { get; set; } = null!;
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}

public class NaviCosmetic
{
    public int            Id          { get; set; }
    public string         Name        { get; set; } = string.Empty;
    public string         Description { get; set; } = string.Empty;
    public CosmeticType   Type        { get; set; } = CosmeticType.Hat;
    public CosmeticRarity Rarity      { get; set; } = CosmeticRarity.Common;
    public int            CoinPrice   { get; set; } = 0;
    public bool           IsExclusive { get; set; } = false;
    public string         AssetSlug   { get; set; } = string.Empty;
    public DateTime       CreatedAt   { get; set; } = DateTime.UtcNow;

    public ICollection<UserCosmetic> UserCosmetics { get; set; } = new List<UserCosmetic>();
}

public enum CosmeticType
{
    Hat         = 1,
    Body        = 2,
    Expression  = 3,
    Fur         = 4,
    Pose        = 5,
    BattleSkin  = 6,
    VictoryAnim = 7
}

public enum CosmeticRarity
{
    Common    = 1,
    Rare      = 2,
    Epic      = 3,
    Legendary = 4
}

public class UserCosmetic
{
    public int          Id         { get; set; }
    public Guid         UserId     { get; set; }
    public User         User       { get; set; } = null!;
    public int          CosmeticId { get; set; }
    public NaviCosmetic Cosmetic   { get; set; } = null!;
    public bool         IsEquipped { get; set; } = false;
    public DateTime     AcquiredAt { get; set; } = DateTime.UtcNow;
}
