namespace Unravel.Domain.Entities;

public class Trail
{
    public int    Id          { get; set; }
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon        { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#7038f2";
    public DifficultyLevel Level { get; set; } = DifficultyLevel.Beginner;
    public bool   IsActive    { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Content>   Contents   { get; set; } = new List<Content>();
    public ICollection<UserTrail> UserTrails { get; set; } = new List<UserTrail>();
}

public enum DifficultyLevel
{
    Beginner     = 1,
    Intermediate = 2,
    Advanced     = 3
}
