using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Social.Ports;

namespace Unravel.API.Controllers;

public record SendPartnershipBody(Guid AddresseeId);

/// <summary>
/// Ideia 1 / UC17 — Parceria ("Novelo de Lã / Parceiro de Trilha"). Convite →
/// aceite → ciclo do novelo (cumpre meta → passa pro parceiro) → recompensa.
/// Delega ao <see cref="IPartnershipService"/>.
/// </summary>
[ApiController]
[Route("api/partnerships")]
[Authorize]
public class PartnershipsController(IPartnershipService partnerships) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/partnerships → parcerias ativas (com estado do novelo)
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => Ok(await partnerships.GetMineAsync(UserId, ct));

    // GET /api/partnerships/requests → convites pendentes (recebidos + enviados)
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(CancellationToken ct)
        => Ok(await partnerships.GetRequestsAsync(UserId, ct));

    // POST /api/partnerships/requests { addresseeId }
    [HttpPost("requests")]
    public async Task<IActionResult> Send([FromBody] SendPartnershipBody body, CancellationToken ct)
    {
        var r = await partnerships.RequestAsync(UserId, body.AddresseeId, ct);
        return r.Outcome switch
        {
            PartnershipOutcome.Ok            => StatusCode(201, new { partnershipId = r.PartnershipId }),
            PartnershipOutcome.CannotSelf    => BadRequest(new { message = "Você não pode formar parceria consigo mesmo." }),
            PartnershipOutcome.NotFound      => NotFound(new { message = "Usuário não encontrado." }),
            PartnershipOutcome.NotFriends    => BadRequest(new { message = "Vocês precisam ser amigos antes de formar uma parceria." }),
            PartnershipOutcome.AlreadyExists => Conflict(new { message = "Já existe uma parceria com esse usuário." }),
            PartnershipOutcome.LimitReached  => Conflict(new { message = "Você atingiu o limite de 3 parcerias ativas." }),
            _                                => BadRequest(),
        };
    }

    // PUT /api/partnerships/requests/{id}/accept
    [HttpPut("requests/{id:int}/accept")]
    public async Task<IActionResult> Accept(int id, CancellationToken ct)
        => Respond(await partnerships.RespondAsync(UserId, id, accept: true, ct));

    // PUT /api/partnerships/requests/{id}/decline
    [HttpPut("requests/{id:int}/decline")]
    public async Task<IActionResult> Decline(int id, CancellationToken ct)
        => Respond(await partnerships.RespondAsync(UserId, id, accept: false, ct));

    // DELETE /api/partnerships/{id} → desfazer parceria
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Break(int id, CancellationToken ct)
    {
        var r = await partnerships.BreakAsync(UserId, id, ct);
        return r.Outcome switch
        {
            PartnershipOutcome.Ok            => NoContent(),
            PartnershipOutcome.NotFound      => NotFound(new { message = "Parceria não encontrada." }),
            PartnershipOutcome.NotAuthorized => StatusCode(403, new { message = "Você não participa dessa parceria." }),
            _                                => BadRequest(),
        };
    }

    private IActionResult Respond(PartnershipActionResult r) => r.Outcome switch
    {
        PartnershipOutcome.Ok            => Ok(new { partnershipId = r.PartnershipId }),
        PartnershipOutcome.NotFound      => NotFound(new { message = "Convite não encontrado." }),
        PartnershipOutcome.NotAuthorized => StatusCode(403, new { message = "Convite não está disponível para responder." }),
        PartnershipOutcome.LimitReached  => Conflict(new { message = "Você atingiu o limite de 3 parcerias ativas." }),
        _                                => BadRequest(),
    };
}
