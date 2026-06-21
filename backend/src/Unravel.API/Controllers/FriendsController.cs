using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Social.Ports;

namespace Unravel.API.Controllers;

public record SendFriendRequestBody(Guid AddresseeId);

/// <summary>
/// PR 64 — mecânicas sociais (Amigos/Parcerias). Grafo de amizades +
/// placar entre amigos. Delega ao <see cref="IFriendshipService"/>.
/// </summary>
[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController(IFriendshipService friends) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/friends → amigos aceitos (placar por XP)
    [HttpGet]
    public async Task<IActionResult> GetFriends(CancellationToken ct)
        => Ok(await friends.GetFriendsAsync(UserId, ct));

    // GET /api/friends/requests → pendentes (recebidos + enviados)
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(CancellationToken ct)
        => Ok(await friends.GetRequestsAsync(UserId, ct));

    // GET /api/friends/search?q=&take=
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await friends.SearchAsync(UserId, q, take, ct));

    // POST /api/friends/requests { addresseeId }
    [HttpPost("requests")]
    public async Task<IActionResult> Send([FromBody] SendFriendRequestBody body, CancellationToken ct)
    {
        var r = await friends.SendRequestAsync(UserId, body.AddresseeId, ct);
        return r.Outcome switch
        {
            FriendActionOutcome.Ok             => StatusCode(201, new { friendshipId = r.FriendshipId }),
            FriendActionOutcome.CannotSelf     => BadRequest(new { message = "Você não pode adicionar a si mesmo." }),
            FriendActionOutcome.NotFound       => NotFound(new { message = "Usuário não encontrado." }),
            FriendActionOutcome.AlreadyFriends => Conflict(new { message = "Vocês já são amigos." }),
            FriendActionOutcome.AlreadyPending => Conflict(new { message = "Já existe um pedido pendente." }),
            FriendActionOutcome.Blocked        => StatusCode(403, new { message = "Não é possível enviar o pedido." }),
            _                                  => BadRequest(),
        };
    }

    // PUT /api/friends/requests/{id}/accept
    [HttpPut("requests/{id:int}/accept")]
    public async Task<IActionResult> Accept(int id, CancellationToken ct)
        => Respond(await friends.RespondAsync(UserId, id, accept: true, ct));

    // PUT /api/friends/requests/{id}/decline
    [HttpPut("requests/{id:int}/decline")]
    public async Task<IActionResult> Decline(int id, CancellationToken ct)
        => Respond(await friends.RespondAsync(UserId, id, accept: false, ct));

    // DELETE /api/friends/{userId}
    [HttpDelete("{otherUserId:guid}")]
    public async Task<IActionResult> Remove(Guid otherUserId, CancellationToken ct)
    {
        var r = await friends.RemoveAsync(UserId, otherUserId, ct);
        return r.Outcome == FriendActionOutcome.Ok
            ? NoContent()
            : NotFound(new { message = "Amizade não encontrada." });
    }

    private IActionResult Respond(FriendActionResult r) => r.Outcome switch
    {
        FriendActionOutcome.Ok            => Ok(new { friendshipId = r.FriendshipId }),
        FriendActionOutcome.NotFound      => NotFound(new { message = "Pedido não encontrado." }),
        FriendActionOutcome.NotAuthorized => StatusCode(403, new { message = "Pedido não está disponível para responder." }),
        _                                 => BadRequest(),
    };
}
