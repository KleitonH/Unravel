using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;
using Unravel.Domain.Tokens;

namespace Unravel.Infrastructure.Persistence;

public partial class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User>         User         => Set<User>();
    public DbSet<RefreshToken> RefreshToken => Set<RefreshToken>();

    public DbSet<Trail>       Trail       => Set<Trail>();
    public DbSet<Content>     Content     => Set<Content>();
    public DbSet<UserTrail>   UserTrail   => Set<UserTrail>();
    public DbSet<UserContent> UserContent => Set<UserContent>();

    public DbSet<Mastery>            Mastery            => Set<Mastery>();
    public DbSet<GeneratedChallenge> GeneratedChallenge => Set<GeneratedChallenge>();
    public DbSet<JourneySnapshot>    JourneySnapshot    => Set<JourneySnapshot>();
    public DbSet<QuestionForgeJob>     QuestionForgeJob     => Set<QuestionForgeJob>();
    public DbSet<ModeratorGoldItem>    ModeratorGoldItem    => Set<ModeratorGoldItem>();
    public DbSet<UserSeenChallenge>    UserSeenChallenge    => Set<UserSeenChallenge>();
    public DbSet<UserBossFight>            UserBossFight            => Set<UserBossFight>();

    // PR 52 — tokens "centímetros de lã" do moderador
    public DbSet<ModeratorTokenBalance>    ModeratorTokenBalance    => Set<ModeratorTokenBalance>();
    public DbSet<ModeratorTokenTransaction> ModeratorTokenTransaction => Set<ModeratorTokenTransaction>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
        mb.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        mb.Entity<Trail>(e =>
        {
            e.HasKey(t => t.Id);
            // PR 28: slug é único (quando preenchido). Filtro nullable
            // permite legacy trails sem slug coexistirem sem violar índice.
            e.Property(t => t.Slug).HasMaxLength(80);
            e.HasIndex(t => t.Slug)
             .IsUnique()
             .HasFilter("\"slug\" IS NOT NULL");
            e.Property(t => t.Name).HasMaxLength(120).IsRequired();
            e.Property(t => t.Description).HasMaxLength(500);
            e.Property(t => t.Icon).HasMaxLength(10);
            e.Property(t => t.AccentColor).HasMaxLength(20);
            e.Property(t => t.Level).HasConversion<int>();

            // PR 35: Source + OwnerUserId pra trilhas custom de moderador.
            // Índice filtrado pra acelerar "minhas trilhas" do moderador
            // sem onerar queries comuns que ignoram esses campos.
            e.Property(t => t.Source).HasConversion<int>();
            e.HasIndex(t => new { t.Source, t.OwnerUserId })
             .HasFilter("\"owner_user_id\" IS NOT NULL");
        });

        mb.Entity<Content>(e =>
        {
            e.HasKey(c => c.Id);
            // PR 28: slug global (não escopado por trilha) — evita
            // ambiguidade em refs cruzadas (gold set / claim extractor).
            e.Property(c => c.Slug).HasMaxLength(80);
            e.HasIndex(c => c.Slug)
             .IsUnique()
             .HasFilter("\"slug\" IS NOT NULL");
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.Property(c => c.ExternalUrl).HasMaxLength(500);
            e.Property(c => c.Type).HasConversion<int>();
            e.Property(c => c.Level).HasConversion<int>();

            // PR 35: Source pra contents custom. KnowledgeImporter filtra
            // por Source=Git ao re-importar (ver KnowledgeImporter.cs).
            e.Property(c => c.Source).HasConversion<int>();

            // PR 40 — meta de desafios pra desbloquear próximo no mapa.
            // Default 5 setado na entity; coluna NOT NULL.
            e.Property(c => c.ChallengesRequired).HasDefaultValue(5);

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

            // PR 40 — Status persistido pra render do mapa sem JOIN.
            e.Property(uc => uc.Status).HasConversion<int>().HasDefaultValue(UserContentStatus.Available);

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

        // PR 32 — QuestionForgeJob: fila persistida de geração de
        // perguntas. Índices pra (a) próxima Pending por prioridade
        // (dequeue rápido) e (b) dedup por (ContentId, ChunkIndex, ClaimHash).
        mb.Entity<QuestionForgeJob>(e =>
        {
            e.HasKey(j => j.Id);
            e.Property(j => j.ClaimText).IsRequired();
            e.Property(j => j.ClaimHash).HasMaxLength(64).IsRequired();
            e.Property(j => j.Status).HasConversion<int>();
            e.Property(j => j.Priority).HasConversion<int>();
            e.Property(j => j.LastError).HasMaxLength(2000);

            // Dequeue: WHERE status=0 ORDER BY priority DESC, id ASC
            e.HasIndex(j => new { j.Status, j.Priority });
            // Dedup: garante que (Content, Chunk, ClaimHash) só existe 1x
            // entre Pending/Running. Permite Done duplicado (histórico).
            e.HasIndex(j => new { j.ContentId, j.ChunkIndex, j.ClaimHash });

            // PR 52a — index pra GET /forge/batches/{id} listar jobs
            // do batch sem table-scan. Filter nullable pra não inchar
            // o índice com jobs legacy sem batch.
            e.HasIndex(j => j.BatchId).HasFilter("\"batch_id\" IS NOT NULL");
            // PR 52a — index pra GET /forge/batches/recent listar batches
            // do moderador autenticado por EnqueuedAt desc.
            e.HasIndex(j => new { j.EnqueuedByUserId, j.EnqueuedAt })
             .HasFilter("\"enqueued_by_user_id\" IS NOT NULL");
        });

        // PR 52 — tokens "centímetros de lã" pra moderador.
        // Balance é singleton por user (PK = UserId). Transactions = log
        // imutável (PK = Id autoincrement, index por UserId desc).
        mb.Entity<ModeratorTokenBalance>(e =>
        {
            e.HasKey(b => b.UserId);
            e.Property(b => b.BalanceCm).IsRequired();
            e.Property(b => b.UpdatedAt).IsRequired();
        });

        mb.Entity<ModeratorTokenTransaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.UserId).IsRequired();
            e.Property(t => t.DeltaCm).IsRequired();
            e.Property(t => t.Reason).HasConversion<int>();
            e.Property(t => t.Metadata).HasColumnType("jsonb");
            e.HasIndex(t => new { t.UserId, t.CreatedAt });
        });

        // PR 50 — UserBossFight: histórico do desafio final da trilha.
        // Singleton por (UserId, TrailId), preserva melhor score e
        // data da primeira vitória (>=70%).
        mb.Entity<UserBossFight>(e =>
        {
            e.HasKey(b => new { b.UserId, b.TrailId });
            e.Property(b => b.AttemptCount).IsRequired();
            e.Property(b => b.BestScore).IsRequired();
            e.Property(b => b.LastScore).IsRequired();
            e.Property(b => b.LastAttemptAt).IsRequired();
        });

        // PR 37 — UserSeenChallenge: rastreia perguntas geradas já
        // respondidas pelo aluno. Anti-join no Reinforcement Quiz.
        // Chave composta (UserId, GeneratedChallengeId) — UPSERT idempotente.
        mb.Entity<UserSeenChallenge>(e =>
        {
            e.HasKey(s => new { s.UserId, s.GeneratedChallengeId });
            e.Property(s => s.SeenAt).IsRequired();

            // FK opcional só pra documentar relacionamento — sem cascata
            // (preservamos histórico mesmo se o challenge for desabilitado).
            e.HasOne(s => s.GeneratedChallenge)
             .WithMany()
             .HasForeignKey(s => s.GeneratedChallengeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // PR 33d — ModeratorGoldItem: gold curado por moderador,
        // complementa o gold YAML estático. Lido pelo ForgeEvaluator
        // junto com o YAML.
        mb.Entity<ModeratorGoldItem>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Prompt).IsRequired();
            e.Property(g => g.CorrectAnswer).IsRequired();
            e.Property(g => g.DistractorsJson).IsRequired();

            e.HasOne(g => g.Content)
             .WithMany()
             .HasForeignKey(g => g.ContentId)
             .OnDelete(DeleteBehavior.Cascade);

            // Lookup do evaluator: WHERE ContentId IN (...) AND IsActive
            e.HasIndex(g => new { g.ContentId, g.IsActive });
        });
    }
}
