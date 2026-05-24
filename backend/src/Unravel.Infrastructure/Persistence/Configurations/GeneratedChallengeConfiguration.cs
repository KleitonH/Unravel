using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unravel.Domain.Forge;

namespace Unravel.Infrastructure.Persistence.Configurations;

internal sealed class GeneratedChallengeConfiguration : IEntityTypeConfiguration<GeneratedChallenge>
{
    public void Configure(EntityTypeBuilder<GeneratedChallenge> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();

        e.Property(x => x.Prompt)              .HasMaxLength(500).IsRequired();
        e.Property(x => x.BodyJson)            .HasColumnType("text").IsRequired();
        e.Property(x => x.Strategy)            .HasConversion<int>();
        e.Property(x => x.EstimatedDifficulty) .IsRequired();
        e.Property(x => x.ServedCount)         .IsRequired();
        e.Property(x => x.CorrectRate)         .IsRequired();
        e.Property(x => x.IsActive)            .IsRequired();
        e.Property(x => x.CreatedAt)           .IsRequired();

        // Acesso dominante: "todas perguntas ativas de um Content, menos
        // vistas primeiro". O índice (ContentId, IsActive, ServedCount)
        // cobre exatamente este padrão.
        e.HasIndex(x => new { x.ContentId, x.IsActive, x.ServedCount });

        // Sem FK explícita pra Content: se um Content for desativado, queremos
        // que as perguntas geradas persistam para análise histórica de
        // qualidade. O filtro IsActive na query lida com o ciclo de vida.
    }
}
