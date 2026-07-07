using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unravel.Domain.Gamification;

namespace Unravel.Infrastructure.Persistence.Configurations;

internal sealed class UserDailyQuestConfiguration : IEntityTypeConfiguration<UserDailyQuest>
{
    public void Configure(EntityTypeBuilder<UserDailyQuest> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();

        e.Property(x => x.UserId).IsRequired();
        e.Property(x => x.QuestDate).IsRequired();
        e.Property(x => x.QuestKey).HasMaxLength(64).IsRequired();
        e.Property(x => x.Target).IsRequired();
        e.Property(x => x.Progress).IsRequired();
        // CompletedAt nullable de propósito (missão em andamento).

        // Idempotência da atribuição + acesso dominante ("missões de hoje do user").
        e.HasIndex(x => new { x.UserId, x.QuestDate, x.QuestKey }).IsUnique();
    }
}
