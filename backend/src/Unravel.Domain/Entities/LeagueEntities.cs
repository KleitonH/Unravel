namespace Unravel.Domain.Entities;

/// <summary>
/// PR 66 — Ligas semanais (estilo Duolingo). Faixas de prestígio; o aluno
/// acumula XP da semana e, ao virar a semana, sobe/desce de faixa conforme
/// a posição no grupo do seu tier.
/// </summary>
public enum LeagueTier
{
    Bronze   = 0,
    Prata    = 1,
    Ouro     = 2,
    Diamante = 3,
    Mestre   = 4,
}

/// <summary>
/// Estado do aluno na liga. XP da semana = User.Xp − BaselineXp (capturado na
/// segunda-feira). WeekKey identifica a semana corrente (data da segunda, ISO
/// "yyyy-MM-dd"). PreviousResult/Rank guardam o desfecho da semana anterior
/// pra exibir banner ("subiu/desceu de liga").
/// </summary>
public class UserLeague
{
    public Guid       UserId         { get; set; }
    public LeagueTier Tier           { get; set; } = LeagueTier.Bronze;
    public string     WeekKey        { get; set; } = string.Empty;
    public int        BaselineXp     { get; set; }
    public int?       PreviousRank   { get; set; }
    public string?    PreviousResult { get; set; } // "promoted" | "relegated" | "stayed"
    public DateTime   UpdatedAt      { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
