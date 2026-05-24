using Microsoft.Extensions.Logging;
using Unravel.Application.Journey.Ports;

namespace Unravel.Infrastructure.Journey;

/// <summary>
/// Implementação placeholder do <see cref="IJourneyEventBus"/>: apenas loga.
/// O PR 8 substitui (ou registra em paralelo) uma implementação SignalR
/// que empurra para clientes conectados.
/// </summary>
public sealed class LoggingJourneyEventBus : IJourneyEventBus
{
    private readonly ILogger<LoggingJourneyEventBus> _log;
    public LoggingJourneyEventBus(ILogger<LoggingJourneyEventBus> log) => _log = log;

    public Task PublishAsync(JourneyEvent evt, CancellationToken ct = default)
    {
        _log.LogInformation("JourneyEvent: {Event}", evt);
        return Task.CompletedTask;
    }
}
