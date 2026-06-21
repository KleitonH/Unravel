namespace Unravel.Application.Notifications.Ports;

/// <summary>
/// PR 70 — lembrete de hábito. Job diário que notifica quem tem ofensiva
/// ativa mas ainda não estudou hoje ("ofensiva em risco"). Idempotente por dia.
/// </summary>
public interface IHabitReminderService
{
    /// <summary>Gera lembretes de ofensiva-em-risco. Retorna quantos foram criados.</summary>
    Task<int> RunAsync(DateTime now, CancellationToken ct = default);
}
