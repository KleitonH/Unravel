using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Social.Ports;

namespace Unravel.API.Controllers;

public record CreateCaixinhaBody(string Name, string? Emblem);
public record PostMuralBody(string Text);

/// <summary>
/// PR 65 — Caixinha de Gatos (clã/grupo). Painel, criar/entrar/sair,
/// expulsar, mural e ranking entre caixinhas. Delega ao ICaixinhaService.
/// </summary>
[ApiController]
[Route("api/caixinhas")]
[Authorize]
public class CaixinhasController(ICaixinhaService caixinhas) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/caixinhas/mine → caixinha do usuário (204 se nenhuma)
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var dto = await caixinhas.GetMineAsync(UserId, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    // GET /api/caixinhas?q=&take= → buscar caixinhas pra entrar
    [HttpGet]
    public async Task<IActionResult> Browse([FromQuery] string? q, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await caixinhas.BrowseAsync(q, take, ct));

    // GET /api/caixinhas/leaderboard?top=
    [HttpGet("leaderboard")]
    public async Task<IActionResult> Leaderboard([FromQuery] int top = 10, CancellationToken ct = default)
        => Ok(await caixinhas.LeaderboardAsync(top, ct));

    // POST /api/caixinhas { name, emblem }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCaixinhaBody body, CancellationToken ct)
    {
        var r = await caixinhas.CreateAsync(UserId, body.Name, body.Emblem, ct);
        return r.Outcome switch
        {
            CaixinhaOutcome.Ok           => StatusCode(201, new { caixinhaId = r.CaixinhaId }),
            CaixinhaOutcome.AlreadyInOne => Conflict(new { message = "Você já está em uma caixinha." }),
            CaixinhaOutcome.NameTooShort => BadRequest(new { message = "O nome precisa de ao menos 2 letras." }),
            _                            => BadRequest(),
        };
    }

    // POST /api/caixinhas/{id}/join
    [HttpPost("{id:int}/join")]
    public async Task<IActionResult> Join(int id, CancellationToken ct)
    {
        var r = await caixinhas.JoinAsync(UserId, id, ct);
        return r.Outcome switch
        {
            CaixinhaOutcome.Ok           => Ok(new { caixinhaId = r.CaixinhaId }),
            CaixinhaOutcome.AlreadyInOne => Conflict(new { message = "Você já está em uma caixinha." }),
            CaixinhaOutcome.Full         => Conflict(new { message = "Esta caixinha está cheia." }),
            CaixinhaOutcome.NotFound     => NotFound(new { message = "Caixinha não encontrada." }),
            _                            => BadRequest(),
        };
    }

    // POST /api/caixinhas/leave
    [HttpPost("leave")]
    public async Task<IActionResult> Leave(CancellationToken ct)
    {
        var r = await caixinhas.LeaveAsync(UserId, ct);
        return r.Outcome switch
        {
            CaixinhaOutcome.Ok        => Ok(new { disbanded = false }),
            CaixinhaOutcome.Disbanded => Ok(new { disbanded = true }),
            CaixinhaOutcome.NotInAny  => NotFound(new { message = "Você não está em uma caixinha." }),
            _                         => BadRequest(),
        };
    }

    // DELETE /api/caixinhas/members/{userId} → líder expulsa
    [HttpDelete("members/{targetUserId:guid}")]
    public async Task<IActionResult> Kick(Guid targetUserId, CancellationToken ct)
    {
        var r = await caixinhas.KickAsync(UserId, targetUserId, ct);
        return r.Outcome switch
        {
            CaixinhaOutcome.Ok        => NoContent(),
            CaixinhaOutcome.NotLeader => StatusCode(403, new { message = "Apenas o líder pode remover membros." }),
            CaixinhaOutcome.NotMember => NotFound(new { message = "Membro não encontrado." }),
            _                         => BadRequest(),
        };
    }

    // POST /api/caixinhas/mural { text }
    [HttpPost("mural")]
    public async Task<IActionResult> PostMural([FromBody] PostMuralBody body, CancellationToken ct)
    {
        var r = await caixinhas.PostMessageAsync(UserId, body.Text, ct);
        return r.Outcome switch
        {
            CaixinhaOutcome.Ok           => StatusCode(201, new { ok = true }),
            CaixinhaOutcome.NotInAny     => NotFound(new { message = "Você não está em uma caixinha." }),
            CaixinhaOutcome.EmptyMessage => BadRequest(new { message = "Mensagem vazia." }),
            _                            => BadRequest(),
        };
    }
}
