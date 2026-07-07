namespace Unravel.Domain.Gamification;

/// <summary>
/// Tipos de atividade que o aluno pode fazer e que alimentam missões diárias.
/// Cada submissão de estudo emite uma dessas; o motor de missões casa o tipo
/// com as missões do dia. Extensível conforme instrumentamos novas telas
/// (Arena, Quiz ao Vivo, etc — hoje só o que passa pelo funil de submit).
/// </summary>
public enum ActivityKind
{
    /// <summary>Respondeu uma pergunta de quiz (qualquer resultado).</summary>
    QuizAnswered = 1,

    /// <summary>Acertou uma pergunta de quiz.</summary>
    QuizCorrect = 2,

    /// <summary>Enfrentou um Boss (submeteu uma boss fight).</summary>
    BossFought = 3,
}

/// <summary>
/// Definição estática de uma missão diária. O catálogo vive em código
/// (<see cref="DailyQuestCatalog"/>); só o progresso do usuário é persistido
/// (<see cref="UserDailyQuest"/>).
/// </summary>
public sealed record DailyQuestDefinition(
    string       Key,
    ActivityKind Activity,
    int          Target,
    string       Title,
    string       Description,
    string       Icon);

/// <summary>
/// Progresso de um usuário numa missão específica de um dia. Persistido.
/// Único por (UserId, QuestDate, QuestKey).
/// </summary>
public sealed class UserDailyQuest
{
    public int       Id          { get; set; }
    public Guid      UserId      { get; set; }
    /// <summary>Componente de data (UTC) a que a missão pertence.</summary>
    public DateTime  QuestDate   { get; set; }
    public string    QuestKey    { get; set; } = "";
    public int       Target      { get; set; }
    public int       Progress    { get; set; }
    /// <summary>Preenchido quando a missão fecha (crédito social já aplicado).</summary>
    public DateTime? CompletedAt { get; set; }

    public bool IsComplete => CompletedAt is not null;
}
