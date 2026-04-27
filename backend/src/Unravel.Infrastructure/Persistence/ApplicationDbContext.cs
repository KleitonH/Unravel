using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;

namespace Unravel.Infrastructure.Persistence;

public partial class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User>         User         => Set<User>();
    public DbSet<RefreshToken> RefreshToken => Set<RefreshToken>();

    public DbSet<Trail>       Trail       => Set<Trail>();
    public DbSet<Content>     Content     => Set<Content>();
    public DbSet<UserTrail>   UserTrail   => Set<UserTrail>();
    public DbSet<UserContent> UserContent => Set<UserContent>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
        mb.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        mb.Entity<Trail>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(120).IsRequired();
            e.Property(t => t.Description).HasMaxLength(500);
            e.Property(t => t.Icon).HasMaxLength(10);
            e.Property(t => t.AccentColor).HasMaxLength(20);
            e.Property(t => t.Level).HasConversion<int>();
        });

        mb.Entity<Content>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.Property(c => c.ExternalUrl).HasMaxLength(500);
            e.Property(c => c.Type).HasConversion<int>();
            e.Property(c => c.Level).HasConversion<int>();

            e.HasOne(c => c.Trail)
             .WithMany(t => t.Contents)
             .HasForeignKey(c => c.TrailId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<UserTrail>(e =>
        {
            e.HasKey(ut => ut.Id);
            e.HasIndex(ut => new { ut.UserId, ut.TrailId }).IsUnique();

            e.HasOne(ut => ut.User)
             .WithMany()
             .HasForeignKey(ut => ut.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ut => ut.Trail)
             .WithMany(t => t.UserTrails)
             .HasForeignKey(ut => ut.TrailId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<UserContent>(e =>
        {
            e.HasKey(uc => uc.Id);
            e.HasIndex(uc => new { uc.UserId, uc.ContentId }).IsUnique();

            e.HasOne(uc => uc.User)
             .WithMany()
             .HasForeignKey(uc => uc.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(uc => uc.Content)
             .WithMany(c => c.UserContents)
             .HasForeignKey(uc => uc.ContentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureGamification(mb);
    }
}
