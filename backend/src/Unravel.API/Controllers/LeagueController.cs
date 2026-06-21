using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Social.Ports;

namespace Unravel.API.Controllers;

/// <summary>PR 66 — liga semanal do aluno (estilo Duolingo).</summary>
[ApiController]
[Route("api/league")]
[Authorize]
public class LeagueController(ILeagueService league) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/league → minha liga na semana corrente
    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken ct)
        => Ok(await league.GetMyLeagueAsync(UserId, DateTime.UtcNow, ct));
}
