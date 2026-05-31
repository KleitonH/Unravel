namespace Unravel.Domain.Entities;

public class Content
{
    public int     Id          { get; set; }

    /// <summary>
    /// Identificador estável para upsert via KnowledgeImporter (PR 28).
    /// Nullable para retrocompatibilidade com conteúdos criados pelo
    /// TrailSeeder antes da migration AddSlugToTrailAndContent. Quando
    /// preenchido, é único na tabela inteira (não escopado por trilha)
    /// para evitar ambiguidade em referências cruzadas (gold set, claim
    /// extractor, forge queue).
    /// </summary>
    public string? Slug        { get; set; }

    public string  Title       { get; set; } = string.Empty;
    public string  Body        { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public ContentType     Type  { get; set; } = ContentType.Article;
    public DifficultyLevel Level { get; set; } = DifficultyLevel.Beginner;
    public int     Order      { get; set; }
    public bool    IsActive   { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int   TrailId { get; set; }
    public Trail Trail   { get; set; } = null!;

    public ICollection<UserContent> UserContents { get; set; } = new List<UserContent>();
}

public enum ContentType
{
    Article  = 1,
    Video    = 2,
    Exercise = 3
}
