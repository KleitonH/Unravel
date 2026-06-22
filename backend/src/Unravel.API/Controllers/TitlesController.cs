using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Achievements.Ports;

namespace Unravel.API.Controllers;

/// <summary>
/// Títulos desbloqueáveis + ranking global (Ideia 5). Catálogo com flags
/// owned/active; ativar um título possuído; avaliar concessões; ranking por XP.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class TitlesController(ITitleService titles) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/titles — catálogo com owned/active pro usuário
    [HttpGet("titles")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await titles.ListAsync(UserId, ct));

    // PUT /api/titles/{id}/activate  (id=0 limpa o título ativo)
    [HttpPut("titles/{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var r = await titles.ActivateAsync(UserId, id, ct);
        return r switch
        {
            ActivateTitleOutcome.Ok       => Ok(new { activated = id }),
            ActivateTitleOutcome.NotOwned => StatusCode(403, new { message = "Você ainda não desbloqueou esse título." }),
            _                             => NotFound(new { message = "Título não encontrado." }),
        };
    }

    // POST /api/titles/evaluate — concede os títulos já merecidos
    [HttpPost("titles/evaluate")]
    public async Task<IActionResult> Evaluate(CancellationToken ct)
        => Ok(new { granted = await titles.EvaluateAsync(UserId, DateTime.UtcNow, ct) });

    // GET /api/ranking/global
    [HttpGet("ranking/global")]
    public async Task<IActionResult> GlobalRanking([FromQuery] int top = 50, CancellationToken ct = default)
        => Ok(await titles.GlobalRankingAsync(top, ct));
}
