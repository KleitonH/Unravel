namespace Unravel.Domain.Entities;

/// <summary>
/// Critério que desbloqueia um título. <c>Manual</c> = concedido por evento
/// específico (não avaliado pelo motor genérico). Os demais são avaliados
/// contra estatísticas do usuário em <c>TitleService.EvaluateAsync</c>.
/// </summary>
public enum TitleCriterion
{
    Manual     = 0,
    StreakDays = 1, // ofensiva ≥ Threshold
    ArenaWins  = 2, // vitórias na Arena ≥ Threshold
    XpTotal    = 3, // XP acumulado ≥ Threshold
}

/// <summary>
/// Catálogo de títulos desbloqueáveis (Ideia 5). Diferente de <c>Badge</c>
/// (conquista visual = "tag"), o título é textual e exibido junto ao nome do
/// usuário (<c>User.ActiveTitle</c>). Temática gato + jargão de TI.
/// </summary>
public class Title
{
    public int            Id        { get; set; }
    public string         Code      { get; set; } = string.Empty; // slug único (idempotência do seed)
    public string         Text      { get; set; } = string.Empty; // ex.: "CSSiamês Profissional"
    public BadgeCategory  Category  { get; set; } = BadgeCategory.Achievement;
    public TitleCriterion Criterion { get; set; } = TitleCriterion.Manual;
    public int            Threshold { get; set; }                 // valor do critério (quando numérico)

    public ICollection<UserTitle> UserTitles { get; set; } = new List<UserTitle>();
}

/// <summary>Título desbloqueado por um usuário.</summary>
public class UserTitle
{
    public int      Id       { get; set; }
    public Guid     UserId   { get; set; }
    public int      TitleId  { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

    public Title? Title { get; set; }
}
