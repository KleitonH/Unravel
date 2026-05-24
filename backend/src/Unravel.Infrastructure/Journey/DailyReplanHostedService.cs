using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unravel.Application.Journey;

namespace Unravel.Infrastructure.Journey;

/// <summary>
/// BackgroundService que dispara o <see cref="DailyReplanService"/> uma vez
/// por dia, alinhado à virada UTC (00:05 — folga de 5 min após meia-noite
/// para reduzir contention com eventuais jobs externos que rodem em :00).
///
/// <para><b>Por que IServiceScopeFactory</b>: o service depende de
/// repositórios scoped (DbContext); este background service vive como
/// singleton durante toda a vida da app. Cada disparo abre um escopo
/// próprio.</para>
///
/// <para><b>Por que não Hangfire/Quartz</b>: para uma única tarefa diária
/// com requisitos modestos, BackgroundService nativo é zero-dependency e
/// suficiente. Se o time precisar de retries, dashboards, distributed
/// locks, etc., migrar pra Quartz vira refactor pontual de uma classe.</para>
/// </summary>
public sealed class DailyReplanHostedService : BackgroundService
{
    private readonly IServiceScopeFactory                 _scopeFactory;
    private readonly ILogger<DailyReplanHostedService>    _log;

    /// <summary>Hora UTC da virada do dia. 00:05 = 5 min após meia-noite UTC.</summary>
    private static readonly TimeSpan TargetUtc = new(0, 5, 0);

    public DailyReplanHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DailyReplanHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _log          = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("DailyReplan hosted service starting; target UTC time = {Target}", TargetUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextDelay(DateTime.UtcNow);
            _log.LogInformation("Next daily replan in {Delay}", delay);

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
                var svc = scope.ServiceProvider.GetRequiredService<DailyReplanService>();
                var report = await svc.RunAsync(DateTime.UtcNow, stoppingToken);
                _log.LogInformation("Daily replan report: {Report}", report);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha no ciclo do DailyReplan; tentaremos novamente no próximo disparo.");
            }
        }

        _log.LogInformation("DailyReplan hosted service stopping.");
    }

    /// <summary>Atraso até o próximo disparo. Se já passou da hora alvo
    /// hoje, mira no próximo dia. Função pura — testável.</summary>
    public static TimeSpan NextDelay(DateTime nowUtc)
    {
        var target = nowUtc.Date + TargetUtc;
        if (target <= nowUtc) target = target.AddDays(1);
        return target - nowUtc;
    }
}
