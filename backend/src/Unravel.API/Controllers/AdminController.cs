using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Journey;

namespace Unravel.API.Controllers;

/// <summary>
/// Operações administrativas — protegidas por role Moderator. Por enquanto
/// só dispara o <see cref="DailyReplanService"/> manualmente (útil para
/// demo/debug sem esperar 00:05 UTC).
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Moderator")]
public sealed class AdminController : ControllerBase
{
    private readonly DailyReplanService _replan;
    public AdminController(DailyReplanService replan) => _replan = replan;

    /// <summary>Roda o lote de replanejamento <i>agora</i>. Idempotente:
    /// se já rodou hoje, faz upsert dos snapshots (não duplica).
    /// Resposta inclui o relatório do lote.</summary>
    [HttpPost("replan-now")]
    public async Task<IActionResult> ReplanNow(CancellationToken ct)
    {
        var report = await _replan.RunAsync(DateTime.UtcNow, ct);
        return Ok(report);
    }
}
