using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Gamification.Ports;

namespace Unravel.API.Controllers;

/// <summary>
/// Missões diárias do aluno. As missões são a unidade de progresso social —
/// concluir uma credita novelo + caixinha (ver <see cref="IActivitySink"/>).
/// </summary>
[ApiController]
[Route("api/quests")]
[Authorize]
public class QuestsController(IDailyQuestService quests) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/quests/today → missões de hoje (com progresso e conclusão)
    [HttpGet("today")]
    public async Task<IActionResult> GetToday(CancellationToken ct)
        => Ok(await quests.GetTodayAsync(UserId, DateTime.UtcNow, ct));
}
