namespace Unravel.Domain.Entities;

public class Trail
{
    public int    Id          { get; set; }

    /// <summary>
    /// Identificador estável para upsert via KnowledgeImporter (PR 28).
    /// Nullable para retrocompatibilidade com trilhas criadas via TrailSeeder
    /// antes da migration AddSlugToTrailAndContent. Trilhas novas importadas
    /// de markdown sempre têm slug. Quando preenchido, é único.
    /// </summary>
    public string? Slug        { get; set; }

    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon        { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#7038f2";
    public DifficultyLevel Level { get; set; } = DifficultyLevel.Beginner;
    public bool   IsActive    { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// PR 35 — origem da trilha. <see cref="ContentSource.Git"/> (default)
    /// é seedada via filesystem e protegida contra edição via API.
    /// <see cref="ContentSource.ModeratorCustom"/> é criada via UI admin
    /// e pode ser editada/deletada pelo dono ou role Admin.
    /// </summary>
    public ContentSource Source { get; set; } = ContentSource.Git;

    /// <summary>
    /// PR 35 — moderador autor da trilha (preenchido só pra <c>Source =
    /// ModeratorCustom</c>). Null pra trilhas Git (não há dono individual).
    /// Aluno vê trilhas Git globais + custom publicadas (tenancy futura
    /// pode restringir por owner).
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>
    /// PR 35 — trilhas custom começam como rascunho não-publicado.
    /// Aluno só vê trilhas <c>IsPublished=true AND IsActive=true</c>.
    /// Trilhas Git são sempre publicadas (controle é via flag IsActive
    /// no manifest).
    /// </summary>
    public bool IsPublished { get; set; } = true;

    public ICollection<Content>   Contents   { get; set; } = new List<Content>();
    public ICollection<UserTrail> UserTrails { get; set; } = new List<UserTrail>();
}

public enum DifficultyLevel
{
    Beginner     = 1,
    Intermediate = 2,
    Advanced     = 3
}
