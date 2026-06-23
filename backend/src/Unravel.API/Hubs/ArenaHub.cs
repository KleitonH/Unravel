using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Unravel.Application.Arena.Ports;

namespace Unravel.API.Hubs;

/// <summary>
/// Hub da Arena (PvP) — <c>/hubs/arena</c>, JWT via header ou
/// <c>?access_token=</c>. Fino: a lógica/estado fica no
/// <see cref="IArenaService"/>; o hub roteia e empurra pro grupo da partida
/// (<c>arena:{matchId}</c>). Diferente do Quiz ao Vivo, NÃO há host: a rodada
/// avança sozinha quando os dois jogadores respondem.
///
/// <para><b>Eventos no cliente</b>: <c>Match</c>, <c>RoundStarted</c>,
/// <c>AnswerResult</c>, <c>OpponentAnswered</c>, <c>RoundResult</c>,
/// <c>MatchFinished</c>.</para>
/// </summary>
[Authorize]
public sealed class ArenaHub(IArenaService arena) : Hub
{
    public static string Group(int matchId) => $"arena:{matchId}";

    private Guid UserId => Guid.Parse(
        Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Entra na sala da partida e recebe o estado atual + a rodada vigente.</summary>
    public async Task JoinMatch(int matchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(matchId));
        await Clients.Caller.SendAsync("Match", await arena.GetMatchAsync(matchId));
        var round = await arena.CurrentRoundAsync(matchId);
        if (round is not null) await Clients.Caller.SendAsync("RoundStarted", round);
    }

    /// <summary>Envia a resposta da rodada. Quando os dois respondem, apura e
    /// empurra o resultado + a próxima rodada (ou o fim) pra sala.</summary>
    public async Task SubmitAnswer(int matchId, int roundIndex, int selectedIndex)
    {
        var r = await arena.SubmitAnswerAsync(matchId, UserId, roundIndex, selectedIndex, DateTime.UtcNow);
        await Clients.Caller.SendAsync("AnswerResult", r);

        if (r.Accepted)
            await Clients.OthersInGroup(Group(matchId)).SendAsync("OpponentAnswered", new { roundIndex });

        if (!r.RoundResolved) return;

        // Rodada apurada (ambos responderam): revela e avança.
        var result = await arena.RoundResultAsync(matchId, roundIndex);
        await Clients.Group(Group(matchId)).SendAsync("RoundResult", result);

        if (r.MatchFinished)
        {
            await Clients.Group(Group(matchId)).SendAsync("MatchFinished", await arena.GetMatchAsync(matchId));
        }
        else
        {
            var next = await arena.CurrentRoundAsync(matchId);
            if (next is not null) await Clients.Group(Group(matchId)).SendAsync("RoundStarted", next);
        }
    }
}
