using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;

namespace Unravel.Infrastructure.Forge;

/// <summary>
/// Roda a auto-desativação de perguntas com taxa extrema uma vez por
/// semana (segunda-feira 01:00 UTC — 1h depois do cron de replanejamento
/// pra não competir por recursos).
///
/// <para>Mesmo padrão do <c>DailyReplanHostedService</c> (PR 7): BackgroundService
/// nativo, IServiceScopeFactory pra abrir escopo a cada disparo, try/catch
/// envolvente para falhas pontuais não matarem o serviço.</para>
/// </summary>
public sealed class GeneratedChallengeMaintenanceHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GeneratedChallengeMaintenanceHostedService> _log;

    /// <summary>01:00 UTC, segunda-feira. Folga de 1h após o cron diário
    /// (PR 7 dispara em 00:05) — não competimos pelo DB nem em alta-carga.</summary>
    private static readonly TimeSpan TargetTimeUtc = new(1, 0, 0);
    private const DayOfWeek TargetDay = DayOfWeek.Monday;

    public GeneratedChallengeMaintenanceHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<GeneratedChallengeMaintenanceHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _log          = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "GeneratedChallenge maintenance hosted service starting; target = {Day} at {Time} UTC",
            TargetDay, TargetTimeUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextDelay(DateTime.UtcNow);
            _log.LogInformation("Next maintenance cycle in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IGeneratedChallengeMaintenance>();
                var report = await svc.AutoDisableExtremesAsync(ct: stoppingToken);
                _log.LogInformation("Maintenance report: {Report}", report);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha no ciclo de manutenção; tentaremos no próximo disparo.");
            }
        }

        _log.LogInformation("GeneratedChallenge maintenance hosted service stopping.");
    }

    /// <summary>Atraso até o próximo TargetDay em TargetTimeUtc. Função pura
    /// — testável sem mocks de clock.</summary>
    public static TimeSpan NextDelay(DateTime nowUtc)
    {
        var target = nowUtc.Date + TargetTimeUtc;
        // Avança até cair em TargetDay e ser estritamente futuro.
        while (target.DayOfWeek != TargetDay || target <= nowUtc)
            target = target.AddDays(1);
        return target - nowUtc;
    }
}
