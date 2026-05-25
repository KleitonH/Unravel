using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unravel.Application.Forge.Ports;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Forge;

/// <summary>
/// Implementação EF de <see cref="IGeneratedChallengeMaintenance"/>. Usa
/// <c>ExecuteUpdate</c> para fazer as duas desativações em SQL atômico,
/// sem materializar — fundamental porque o lote pode crescer.
/// </summary>
public sealed class GeneratedChallengeMaintenance : IGeneratedChallengeMaintenance
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<GeneratedChallengeMaintenance> _log;

    public GeneratedChallengeMaintenance(
        ApplicationDbContext db,
        ILogger<GeneratedChallengeMaintenance> log)
    {
        _db  = db;
        _log = log;
    }

    public async Task<AutoDisableReport> AutoDisableExtremesAsync(
        int minServed = 20,
        double lowerBound = 0.10,
        double upperBound = 0.95,
        CancellationToken ct = default)
    {
        var tooHard = await _db.GeneratedChallenge
            .Where(g => g.IsActive && g.ServedCount >= minServed && g.CorrectRate < lowerBound)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsActive, false), ct);

        var tooEasy = await _db.GeneratedChallenge
            .Where(g => g.IsActive && g.ServedCount >= minServed && g.CorrectRate > upperBound)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsActive, false), ct);

        if (tooHard > 0 || tooEasy > 0)
            _log.LogInformation(
                "Auto-disabled {TooHard} too-hard + {TooEasy} too-easy generated challenges " +
                "(min served={MinServed}, bounds=[{Lower}, {Upper}])",
                tooHard, tooEasy, minServed, lowerBound, upperBound);

        return new AutoDisableReport(tooHard, tooEasy);
    }
}
