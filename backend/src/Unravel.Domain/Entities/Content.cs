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

    /// <summary>
    /// PR 35 — origem do conteúdo. <see cref="ContentSource.Git"/> é
    /// upsertado pelo <c>KnowledgeImporter</c>. <see cref="ContentSource.ModeratorCustom"/>
    /// é criado via API e <b>ignorado</b> pelo re-import (pra não ser
    /// sobrescrito acidentalmente quando o filesystem não tem nada
    /// correspondente).
    /// </summary>
    public ContentSource Source { get; set; } = ContentSource.Git;

    /// <summary>
    /// PR 35 — última edição via API (null pra Git, populado em PATCH
    /// de Content custom). Usado pra invalidar/reprocessar perguntas
    /// que podem ter ficado desalinhadas com o chunk editado.
    /// </summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>
    /// PR 35 — quem fez a última edição via API. Auditoria simples;
    /// histórico completo de revisões fica fora do MVP.
    /// </summary>
    public Guid? EditedByUserId { get; set; }

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
