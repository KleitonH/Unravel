namespace Unravel.Domain.Gamification;

/// <summary>
/// Catálogo estático de missões diárias e a seleção rotativa do dia.
///
/// <para>A seleção é <b>determinística por data</b> — todos os usuários recebem
/// o mesmo conjunto num dado dia, e o conjunto muda a cada dia (pool rotativo).
/// Sendo pura (sem clock interno nem BD), é reproduzível em teste.</para>
///
/// <para>A missão-espinha <c>answer-5</c> está sempre presente pra garantir que
/// "responder perguntas" sempre gere progresso social; as demais rotacionam.</para>
/// </summary>
public static class DailyQuestCatalog
{
    /// <summary>Quantas missões o aluno recebe por dia.</summary>
    public const int QuestsPerDay = 3;

    /// <summary>Pontos creditados na meta da caixinha por missão concluída.</summary>
    public const int CaixinhaPointsPerQuest = 50;

    /// <summary>
    /// Ordem estável — o índice é usado na rotação determinística. O primeiro
    /// item é a espinha (sempre incluída).
    /// </summary>
    public static readonly IReadOnlyList<DailyQuestDefinition> All = new[]
    {
        new DailyQuestDefinition("answer-5",  ActivityKind.QuizAnswered, 5,  "Responda 5 perguntas",  "Responda 5 perguntas em qualquer trilha, reforço ou quiz.", "📝"),
        new DailyQuestDefinition("correct-3", ActivityKind.QuizCorrect,  3,  "Acerte 3 perguntas",    "Acerte 3 perguntas hoje.",                                  "🎯"),
        new DailyQuestDefinition("answer-10", ActivityKind.QuizAnswered, 10, "Responda 10 perguntas", "Maratona: responda 10 perguntas hoje.",                     "🔥"),
        new DailyQuestDefinition("correct-5", ActivityKind.QuizCorrect,  5,  "Acerte 5 perguntas",    "Acerte 5 perguntas hoje.",                                  "⭐"),
        new DailyQuestDefinition("boss-1",    ActivityKind.BossFought,   1,  "Enfrente 1 Boss",       "Encare um Boss de qualquer trilha.",                        "👑"),
    };

    private static readonly IReadOnlyDictionary<string, DailyQuestDefinition> ByKey =
        All.ToDictionary(q => q.Key);

    public static DailyQuestDefinition? Find(string key) =>
        ByKey.TryGetValue(key, out var def) ? def : null;

    /// <summary>
    /// Conjunto de missões de uma data (espinha + rotativas). Determinístico:
    /// mesmo <paramref name="date"/> → mesmo conjunto, sempre.
    /// </summary>
    public static IReadOnlyList<DailyQuestDefinition> ForDate(DateTime date)
    {
        var backbone = All[0];
        var rotating = All.Skip(1).ToList();

        // Seed estável derivado da data (dia absoluto). Sem Random pra manter
        // reprodutibilidade e paridade entre usuários.
        var seed  = date.Date.Year * 372 + date.Date.DayOfYear;
        var picks = new List<DailyQuestDefinition>(QuestsPerDay) { backbone };

        var take = Math.Min(QuestsPerDay - 1, rotating.Count);
        for (var i = 0; i < take; i++)
            picks.Add(rotating[(seed + i) % rotating.Count]);

        return picks;
    }
}
