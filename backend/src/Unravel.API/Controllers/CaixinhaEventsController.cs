using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Social.Ports;

namespace Unravel.API.Controllers;

public record CreateEventBody(string Name, string? Theme, DateTime StartsAt, DateTime EndsAt);

/// <summary>
/// PR 65c — eventos entre caixinhas. Criar é restrito ao moderador (a
/// "plataforma" ativa eventos); listar/detalhe/participar pra alunos.
/// </summary>
[ApiController]
[Route("api/caixinhas/events")]
[Authorize]
public class CaixinhaEventsController(ICaixinhaEventService events) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/caixinhas/events
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await events.ListAsync(UserId, DateTime.UtcNow, ct));

    // GET /api/caixinhas/events/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var dto = await events.GetDetailAsync(UserId, id, DateTime.UtcNow, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // POST /api/caixinhas/events  (moderador)
    [HttpPost]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Create([FromBody] CreateEventBody body, CancellationToken ct)
    {
        var r = await events.CreateAsync(UserId, body.Name, body.Theme, body.StartsAt, body.EndsAt, ct);
        return r.Outcome switch
        {
            CaixinhaEventOutcome.Ok           => StatusCode(201, new { eventId = r.EventId }),
            CaixinhaEventOutcome.NameTooShort => BadRequest(new { message = "O nome precisa de ao menos 2 letras." }),
            CaixinhaEventOutcome.InvalidDates => BadRequest(new { message = "A data de fim deve ser depois do início." }),
            _                                 => BadRequest(),
        };
    }

    // POST /api/caixinhas/events/{id}/join  (líder)
    [HttpPost("{id:int}/join")]
    public async Task<IActionResult> Join(int id, CancellationToken ct)
    {
        var r = await events.JoinAsync(UserId, id, DateTime.UtcNow, ct);
        return r.Outcome switch
        {
            CaixinhaEventOutcome.Ok            => Ok(new { eventId = r.EventId }),
            CaixinhaEventOutcome.NotInAny      => NotFound(new { message = "Você não está em uma caixinha." }),
            CaixinhaEventOutcome.NotLeader     => StatusCode(403, new { message = "Apenas o líder inscreve a caixinha." }),
            CaixinhaEventOutcome.NotFound      => NotFound(new { message = "Evento não encontrado." }),
            CaixinhaEventOutcome.NotActive     => Conflict(new { message = "Evento não está ativo." }),
            CaixinhaEventOutcome.AlreadyJoined => Conflict(new { message = "Sua caixinha já está participando." }),
            _                                  => BadRequest(),
        };
    }
}
