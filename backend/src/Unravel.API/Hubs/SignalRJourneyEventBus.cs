using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Unravel.Application.Journey.Ports;

namespace Unravel.API.Hubs;

/// <summary>
/// Implementação SignalR do <see cref="IJourneyEventBus"/>. Substitui o
/// <c>LoggingJourneyEventBus</c> registrado pelo Infrastructure
/// (último <c>AddSingleton</c> com a mesma interface vence) — mantemos
/// o log secundário para observabilidade durante demo/troubleshooting.
///
/// <para>Roteamento: cada evento conhece o <c>UserId</c>; publicamos no
/// grupo <c>user:{userId}</c> via <see cref="IHubContext{T}"/>. Cliente
/// sem assinatura nesse grupo simplesmente não recebe.</para>
///
/// <para>Por que <see cref="IHubContext{T}"/> e não <c>JourneyHub</c>
/// direto: o hub é por-conexão, lifetime curto; o context é singleton
/// thread-safe, certo para uso fora do pipeline HTTP (cron diário,
/// por exemplo).</para>
/// </summary>
public sealed class SignalRJourneyEventBus : IJourneyEventBus
{
    private readonly IHubContext<JourneyHub>          _hub;
    private readonly ILogger<SignalRJourneyEventBus>  _log;

    public SignalRJourneyEventBus(IHubContext<JourneyHub> hub, ILogger<SignalRJourneyEventBus> log)
    {
        _hub = hub;
        _log = log;
    }

    public async Task PublishAsync(JourneyEvent evt, CancellationToken ct = default)
    {
        // Log paralelo (não loga payload completo — observabilidade leve).
        _log.LogInformation("Publishing {EventType} via SignalR", evt.GetType().Name);

        switch (evt)
        {
            case DailyPlanGenerated dpg:
                await _hub.Clients
                    .Group(JourneyHub.UserGroup(dpg.UserId))
                    .SendAsync(nameof(DailyPlanGenerated), dpg, ct);
                break;

            case StreakReset sr:
                await _hub.Clients
                    .Group(JourneyHub.UserGroup(sr.UserId))
                    .SendAsync(nameof(StreakReset), sr, ct);
                break;

            default:
                // Evento novo sem handler? Logar p/ não engolir silenciosamente.
                _log.LogWarning("No SignalR handler for event {EventType}", evt.GetType().FullName);
                break;
        }
    }
}
