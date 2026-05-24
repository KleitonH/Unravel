using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unravel.Domain.Knowledge;

namespace Unravel.Infrastructure.Persistence.Configurations;

internal sealed class JourneySnapshotConfiguration : IEntityTypeConfiguration<JourneySnapshot>
{
    public void Configure(EntityTypeBuilder<JourneySnapshot> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();

        e.Property(x => x.PlanJson).HasColumnType("text").IsRequired();
        e.Property(x => x.PlanDate).IsRequired();
        e.Property(x => x.MetaDia).IsRequired();
        e.Property(x => x.ExtraChallengesPenalty).IsRequired();
        e.Property(x => x.GeneratedAt).IsRequired();
        // MetGoal é nullable de propósito (snapshot do dia atual ainda não
        // foi avaliado pelo cron do dia seguinte).

        // Unique p/ idempotência do upsert. Acesso dominante: "snapshot
        // mais recente do user×trilha". Este índice cobre.
        e.HasIndex(x => new { x.UserId, x.TrailId, x.PlanDate }).IsUnique();
    }
}
