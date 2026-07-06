using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unravel.Domain.Forge;

namespace Unravel.Infrastructure.Persistence.Configurations;

internal sealed class ChallengeFeedbackConfiguration : IEntityTypeConfiguration<ChallengeFeedback>
{
    public void Configure(EntityTypeBuilder<ChallengeFeedback> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedOnAdd();

        e.Property(x => x.Reason)   .HasConversion<int>();
        e.Property(x => x.Status)   .HasConversion<int>();
        e.Property(x => x.Comment)  .HasMaxLength(1000);
        e.Property(x => x.CreatedAt).IsRequired();

        // Badge por pergunta: COUNT WHERE generated_challenge_id=? AND status=0
        e.HasIndex(x => new { x.GeneratedChallengeId, x.Status });
        // Painel do moderador: lista feedbacks de um conteúdo inteiro
        e.HasIndex(x => new { x.ContentId, x.Status });
        // Anti-spam: um aluno sinaliza cada pergunta no máximo 1x
        e.HasIndex(x => new { x.GeneratedChallengeId, x.UserId }).IsUnique();

        // Cascata: se a pergunta gerada for hard-deleted, os feedbacks vão
        // junto. (Na prática usamos soft-delete IsActive=false, então o
        // histórico persiste.)
        e.HasOne<GeneratedChallenge>()
         .WithMany()
         .HasForeignKey(x => x.GeneratedChallengeId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
