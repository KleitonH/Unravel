using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Journey.UseCases;

namespace Unravel.API.Controllers;

/// <summary>
/// Endpoints do algoritmo de organização de jornadas (PR 3). A jornada é
/// recalculada a cada request — o <c>JourneyPlanner</c> é puro/in-memory
/// e o cache do KnowledgeGraph + masteries é barato. Não persistimos o
/// plano em si (snapshot diário é trabalho do PR 7, cron).
/// </summary>
[ApiController]
[Route("api/journey")]
[Authorize]
public sealed class JourneyController : ControllerBase
{
    private readonly GetDailyJourneyUseCase _getDaily;

    private Guid UserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public JourneyController(GetDailyJourneyUseCase getDaily) => _getDaily = getDaily;

    /// <summary>Plano do dia para o usuário autenticado numa trilha. Calcula
    /// no momento da chamada usando o instante atual como <c>asOf</c>.
    /// Retorna 404 se a trilha não existe / está inativa, ou se o usuário
    /// não existe.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> Today([FromQuery] int trailId, CancellationToken ct)
    {
        if (trailId <= 0) return BadRequest(new { message = "trailId é obrigatório." });

        var plan = await _getDaily.ExecuteAsync(UserId, trailId, DateTime.UtcNow, ct);
        return plan is null ? NotFound() : Ok(plan);
    }

    /// <summary>Força recálculo da jornada. Hoje é idêntico a
    /// <see cref="Today"/> (o planner sempre roda na chamada); o endpoint
    /// existe como contrato pro frontend quando o cron diário (PR 7) e o
    /// snapshot persistido entrarem em cena.</summary>
    [HttpPost("replan")]
    public async Task<IActionResult> Replan([FromQuery] int trailId, CancellationToken ct)
    {
        if (trailId <= 0) return BadRequest(new { message = "trailId é obrigatório." });

        var plan = await _getDaily.ExecuteAsync(UserId, trailId, DateTime.UtcNow, ct);
        return plan is null ? NotFound() : Ok(plan);
    }
}
