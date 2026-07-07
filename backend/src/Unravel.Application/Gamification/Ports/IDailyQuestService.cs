namespace Unravel.Application.Gamification.Ports;

/// <summary>Uma missão do dia do ponto de vista do aluno (pra UI).</summary>
public sealed record DailyQuestView(
    string Key,
    string Title,
    string Description,
    string Icon,
    int    Target,
    int    Progress,
    bool   Completed);

/// <summary>
/// Leitura das missões diárias do usuário. Atribui o conjunto do dia (rotativo)
/// na primeira leitura, de forma idempotente.
/// </summary>
public interface IDailyQuestService
{
    Task<IReadOnlyList<DailyQuestView>> GetTodayAsync(Guid userId, DateTime asOfUtc, CancellationToken ct = default);
}
