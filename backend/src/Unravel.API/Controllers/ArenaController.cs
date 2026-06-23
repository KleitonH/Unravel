using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Unravel.API.Hubs;
using Unravel.Application.Arena.Ports;

namespace Unravel.API.Controllers;

public record EnqueueArenaBody(int TrailId);
public record ChallengeArenaBody(Guid OpponentId, int TrailId);
public record SubmitArenaBody(int RoundIndex, int SelectedIndex);

/// <summary>
/// Arena (PvP) — controle REST. Matchmaking por fila ou desafio direto;
/// rodadas com pontuação por acerto+velocidade; ranking. O push em tempo real
/// (SignalR) vem por cima deste mesmo serviço numa etapa seguinte.
/// </summary>
[ApiController]
[Route("api/arena")]
[Authorize]
public class ArenaController(IArenaService arena, IHubContext<ArenaHub> hub) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // POST /api/arena/queue  → pareia ou entra na fila
    [HttpPost("queue")]
    public async Task<IActionResult> Enqueue([FromBody] EnqueueArenaBody body, CancellationToken ct)
    {
        var r = await arena.EnqueueAsync(UserId, body.TrailId, ct);
        if (r.Matched && r.MatchId is int mid) await PushMatchedAsync(mid, ct);
        return Ok(r);
    }

    [HttpDelete("queue")]
    public async Task<IActionResult> LeaveQueue(CancellationToken ct)
    {
        await arena.LeaveQueueAsync(UserId, ct);
        return NoContent();
    }

    // POST /api/arena/challenge  { opponentId, trailId }
    [HttpPost("challenge")]
    public async Task<IActionResult> Challenge([FromBody] ChallengeArenaBody body, CancellationToken ct)
    {
        var r = await arena.ChallengeAsync(UserId, body.OpponentId, body.TrailId, ct);
        return r.Outcome switch
        {
            ArenaActionOutcome.Ok              => StatusCode(201, new { matchId = r.MatchId }),
            ArenaActionOutcome.CannotSelf      => BadRequest(new { message = "Você não pode desafiar a si mesmo." }),
            ArenaActionOutcome.OpponentNotFound=> NotFound(new { message = "Oponente não encontrado." }),
            ArenaActionOutcome.NoQuestions     => BadRequest(new { message = "Essa trilha não tem questões pra batalhar." }),
            _                                  => BadRequest(),
        };
    }

    [HttpPut("matches/{id:int}/accept")]
    public async Task<IActionResult> Accept(int id, CancellationToken ct)
    {
        var r = await arena.RespondChallengeAsync(id, UserId, accept: true, ct);
        if (r.Outcome == ArenaActionOutcome.Ok && r.MatchId is int mid) await PushMatchedAsync(mid, ct);
        return Respond(r);
    }

    [HttpPut("matches/{id:int}/decline")]
    public async Task<IActionResult> Decline(int id, CancellationToken ct)
        => Respond(await arena.RespondChallengeAsync(id, UserId, accept: false, ct));

    [HttpGet("matches/{id:int}")]
    public async Task<IActionResult> GetMatch(int id, CancellationToken ct)
    {
        var m = await arena.GetMatchAsync(id, ct);
        return m is null ? NotFound() : Ok(m);
    }

    [HttpGet("matches/{id:int}/current-round")]
    public async Task<IActionResult> CurrentRound(int id, CancellationToken ct)
    {
        var r = await arena.CurrentRoundAsync(id, ct);
        return r is null ? NoContent() : Ok(r);
    }

    [HttpPost("matches/{id:int}/answer")]
    public async Task<IActionResult> Answer(int id, [FromBody] SubmitArenaBody body, CancellationToken ct)
        => Ok(await arena.SubmitAnswerAsync(id, UserId, body.RoundIndex, body.SelectedIndex, DateTime.UtcNow, ct));

    [HttpGet("matches/{id:int}/round/{orderIndex:int}/result")]
    public async Task<IActionResult> RoundResult(int id, int orderIndex, CancellationToken ct)
    {
        var r = await arena.RoundResultAsync(id, orderIndex, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpGet("ranking")]
    public async Task<IActionResult> Ranking([FromQuery] int top = 20, CancellationToken ct = default)
        => Ok(await arena.RankingAsync(top, ct));

    [HttpGet("my-matches")]
    public async Task<IActionResult> MyMatches(CancellationToken ct)
        => Ok(await arena.MyOpenMatchesAsync(UserId, ct));

    /// <summary>Avisa os dois jogadores que o pareamento aconteceu (push direto
    /// via SignalR) — quem estava na fila/aguardando entra no duelo na hora,
    /// sem polling.</summary>
    private async Task PushMatchedAsync(int matchId, CancellationToken ct)
    {
        var m = await arena.GetMatchAsync(matchId, ct);
        if (m is null) return;
        await hub.Clients.User(m.Player1Id.ToString()).SendAsync("Matched", new { matchId }, ct);
        if (m.Player2Id is Guid p2)
            await hub.Clients.User(p2.ToString()).SendAsync("Matched", new { matchId }, ct);
    }

    private IActionResult Respond(ArenaActionResult r) => r.Outcome switch
    {
        ArenaActionOutcome.Ok            => Ok(new { matchId = r.MatchId }),
        ArenaActionOutcome.NotFound      => NotFound(new { message = "Partida não encontrada." }),
        ArenaActionOutcome.NotAuthorized => StatusCode(403, new { message = "Desafio indisponível pra responder." }),
        ArenaActionOutcome.NoQuestions   => BadRequest(new { message = "Sem questões pra essa trilha." }),
        _                                => BadRequest(),
    };
}
