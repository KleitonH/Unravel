namespace Unravel.Domain.Entities;

public class UserTrail
{
    public int      Id          { get; set; }
    public Guid     UserId      { get; set; }
    public User     User        { get; set; } = null!;
    public int      TrailId     { get; set; }
    public Trail    Trail       { get; set; } = null!;
    public bool     IsActive    { get; set; } = true;
    public int      Progress    { get; set; } = 0;
    public DateTime EnrolledAt  { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
