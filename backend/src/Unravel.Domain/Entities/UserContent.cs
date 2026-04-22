namespace Unravel.Domain.Entities;

public class UserContent
{
    public int      Id          { get; set; }
    public Guid     UserId      { get; set; }
    public User     User        { get; set; } = null!;
    public int      ContentId   { get; set; }
    public Content  Content     { get; set; } = null!;
    public bool     IsCompleted { get; set; } = false;
    public DateTime StartedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
