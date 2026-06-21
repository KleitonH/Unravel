using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unravel.Application.Notifications.Ports;

namespace Unravel.Infrastructure.Notifications;

/// <summary>
/// PR 70 — BackgroundService que dispara o lembrete de hábito uma vez por dia,
/// às 21:00 UTC (fim de tarde/noite no BR), quando faz sentido lembrar quem
/// ainda não estudou. Mesmo padrão do <c>DailyReplanHostedService</c>:
/// singleton que abre um escopo por disparo (service depende de DbContext scoped).
/// </summary>
public sealed class HabitReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory                  _scopeFactory;
    private readonly ILogger<HabitReminderHostedService>   _log;

    /// <summary>Hora UTC do disparo diário (21:00 UTC ≈ 18:00 BRT).</summary>
    private static readonly TimeSpan TargetUtc = new(21, 0, 0);

    public HabitReminderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<HabitReminderHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _log          = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("HabitReminder hosted service starting; target UTC = {Target}", TargetUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextDelay(DateTime.UtcNow);
            _log.LogInformation("Next habit-reminder run in {Delay}", delay);

            try { await Task.Delay(delay, stoppingToken); }
            catch (TaskCanceledException) { break; }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IHabitReminderService>();
                var sent = await svc.RunAsync(DateTime.UtcNow, stoppingToken);
                _log.LogInformation("Habit reminders sent: {Count}", sent);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha no ciclo do HabitReminder; tentaremos no próximo disparo.");
            }
        }

        _log.LogInformation("HabitReminder hosted service stopping.");
    }

    /// <summary>Atraso até o próximo disparo (função pura — testável).</summary>
    public static TimeSpan NextDelay(DateTime nowUtc)
    {
        var target = nowUtc.Date + TargetUtc;
        if (target <= nowUtc) target = target.AddDays(1);
        return target - nowUtc;
    }
}
