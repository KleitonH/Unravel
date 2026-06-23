using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unravel.Application.Gamification.Ports;

namespace Unravel.Infrastructure.Gamification;

/// <summary>
/// UC34 — BackgroundService que recarrega vidas (+1/h até o teto) em lote.
/// Roda a cada <see cref="TickInterval"/>; como o cálculo é por tempo decorrido
/// (não por nº de ticks), a frequência só afeta quão cedo o banco reflete o
/// valor — as leituras de perfil já fazem refill "lazy". Mesmo padrão singleton
/// dos demais hosted services: abre um escopo por disparo (serviço é scoped).
/// </summary>
public sealed class LifeRefillHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LifeRefillHostedService> _log;

    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(10);

    public LifeRefillHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<LifeRefillHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("LifeRefill hosted service starting; tick = {Tick}", TickInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ILifeRefillService>();
                var affected = await svc.RefillAllAsync(DateTime.UtcNow, stoppingToken);
                if (affected > 0) _log.LogInformation("LifeRefill: {Count} usuários ganharam vida.", affected);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha no ciclo do LifeRefill; tentaremos no próximo tick.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }

        _log.LogInformation("LifeRefill hosted service stopping.");
    }
}
