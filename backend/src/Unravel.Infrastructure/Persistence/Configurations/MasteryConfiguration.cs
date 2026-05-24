using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unravel.Domain.Knowledge;

namespace Unravel.Infrastructure.Persistence.Configurations;

internal sealed class MasteryConfiguration : IEntityTypeConfiguration<Mastery>
{
    public void Configure(EntityTypeBuilder<Mastery> e)
    {
        // Chave composta — uma linha por (user, topic). TopicId implica TrailId,
        // mas mantemos TrailId materializado para indexar listagens por trilha
        // sem JOIN.
        e.HasKey(m => new { m.UserId, m.TopicId });

        e.Property(m => m.Score).IsRequired();
        e.Property(m => m.Confidence).IsRequired();
        e.Property(m => m.LastSeenAt).IsRequired();
        e.Property(m => m.SrsIntervalDays).IsRequired();
        e.Property(m => m.EaseFactor).IsRequired();
        e.Property(m => m.TrailId).IsRequired();

        // Padrão de acesso dominante: "todas as masteries de um usuário numa trilha"
        // (planner, relatório, perfil de domínio para Arena). Esse índice cobre.
        e.HasIndex(m => new { m.UserId, m.TrailId });

        // Padrão secundário: "tópicos com revisão vencida até X". Index por
        // (UserId, TrailId, LastSeenAt + SrsIntervalDays) seria ideal mas EF
        // não suporta índice em expressão por padrão; índice em LastSeenAt
        // cobre razoavelmente porque SrsIntervalDays tem cardinalidade baixa.
        e.HasIndex(m => new { m.UserId, m.LastSeenAt });

        // NextDueAt é só ergonomia em memória — não persistir.
        e.Ignore(m => m.NextDueAt);
    }
}
