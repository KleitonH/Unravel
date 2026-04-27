using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;

namespace Unravel.Infrastructure.Persistence;

public partial class ApplicationDbContext
{
    public DbSet<Challenge>       Challenge       => Set<Challenge>();
    public DbSet<ChallengeOption> ChallengeOption => Set<ChallengeOption>();
    public DbSet<UserChallenge>   UserChallenge   => Set<UserChallenge>();
    public DbSet<Badge>           Badge           => Set<Badge>();
    public DbSet<UserBadge>       UserBadge       => Set<UserBadge>();
    public DbSet<NaviCosmetic>    NaviCosmetic    => Set<NaviCosmetic>();
    public DbSet<UserCosmetic>    UserCosmetic    => Set<UserCosmetic>();

    private void ConfigureGamification(ModelBuilder mb)
    {
        mb.Entity<Challenge>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.Property(c => c.Type).HasConversion<int>();
            e.Property(c => c.Level).HasConversion<int>();

            e.HasOne(c => c.Trail)
             .WithMany()
             .HasForeignKey(c => c.TrailId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ChallengeOption>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Text).HasMaxLength(500).IsRequired();

            e.HasOne(o => o.Challenge)
             .WithMany(c => c.Options)
             .HasForeignKey(o => o.ChallengeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<UserChallenge>(e =>
        {
            e.HasKey(uc => uc.Id);
            e.HasIndex(uc => new { uc.UserId, uc.ChallengeId, uc.StartedAt });

            e.HasOne(uc => uc.User)
             .WithMany(u => u.ChallengeAttempts)
             .HasForeignKey(uc => uc.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(uc => uc.Challenge)
             .WithMany(c => c.UserAttempts)
             .HasForeignKey(uc => uc.ChallengeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Badge>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.Name).HasMaxLength(100).IsRequired();
            e.Property(b => b.Category).HasConversion<int>();
        });

        mb.Entity<UserBadge>(e =>
        {
            e.HasKey(ub => ub.Id);
            e.HasIndex(ub => new { ub.UserId, ub.BadgeId }).IsUnique();

            e.HasOne(ub => ub.User)
             .WithMany(u => u.Badges)
             .HasForeignKey(ub => ub.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ub => ub.Badge)
             .WithMany(b => b.UserBadges)
             .HasForeignKey(ub => ub.BadgeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<NaviCosmetic>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Name).HasMaxLength(100).IsRequired();
            e.Property(n => n.Type).HasConversion<int>();
            e.Property(n => n.Rarity).HasConversion<int>();
            e.Property(n => n.AssetSlug).HasMaxLength(100);
        });

        mb.Entity<UserCosmetic>(e =>
        {
            e.HasKey(uc => uc.Id);
            e.HasIndex(uc => new { uc.UserId, uc.CosmeticId }).IsUnique();

            e.HasOne(uc => uc.User)
             .WithMany(u => u.Cosmetics)
             .HasForeignKey(uc => uc.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(uc => uc.Cosmetic)
             .WithMany(c => c.UserCosmetics)
             .HasForeignKey(uc => uc.CosmeticId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<User>(e =>
        {
            e.Property(u => u.ActiveTitle).HasMaxLength(100);
        });
    }
}
