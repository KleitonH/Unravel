using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;

namespace Unravel.Infrastructure.Forge;

/// <summary>
/// Roda o lote noturno do LLM uma vez por dia às 02:00 UTC — depois do
/// cron diário do PR 7 (00:05 UTC) e do maintenance semanal do PR 17
/// (segundas 01:00 UTC). Nessa janela, ninguém compete por CPU/RAM e o
/// LLM pode usar todos os recursos.
///
/// <para>Padrão de implementação igual aos outros hosted services
/// (DailyReplanHostedService, GeneratedChallengeMaintenanceHostedService):
/// IServiceScopeFactory por disparo, try/catch envolvente, NextDelay puro
/// testável.</para>
/// </summary>
public sealed class LlmGenerationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LlmGenerationHostedService> _log;
    private static readonly TimeSpan TargetTimeUtc = new(2, 0, 0);

    public LlmGenerationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<LlmGenerationHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "LLM generation hosted service starting; target UTC time = {Target}",
            TargetTimeUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextDelay(DateTime.UtcNow);
            _log.LogInformation("Next LLM generation cycle in {Delay}", delay);

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
                var svc = scope.ServiceProvider.GetRequiredService<ILlmGenerationOrchestrator>();
                await svc.RunAsync(ct: stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha no ciclo de geração LLM; tentaremos no próximo disparo.");
            }
        }

        _log.LogInformation("LLM generation hosted service stopping.");
    }

    /// <summary>Atraso até o próximo TargetTimeUtc. Função pura, testável.</summary>
    public static TimeSpan NextDelay(DateTime nowUtc)
    {
        var target = nowUtc.Date + TargetTimeUtc;
        if (target <= nowUtc) target = target.AddDays(1);
        return target - nowUtc;
    }
}
