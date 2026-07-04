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

    /// <summary>Entra na sala da partida e recebe o estado atual + a rodada vigente.
    /// Se estava marcado como desconectado, limpa (voltou a tempo) e avisa o oponente.</summary>
    public async Task JoinMatch(int matchId)
    {
        Context.Items["matchId"] = matchId;
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(matchId));
        await arena.ClearDisconnectAsync(matchId, UserId);
        await Clients.OthersInGroup(Group(matchId)).SendAsync("OpponentReturned", new { matchId });
        await Clients.Caller.SendAsync("Match", await arena.GetMatchAsync(matchId));
        var round = await arena.CurrentRoundAsync(matchId);
        if (round is not null) await Clients.Caller.SendAsync("RoundStarted", round);
    }

    /// <summary>Cliente que ficou reivindica a vitória após os 30s de reconexão do
    /// oponente. Idempotente no servidor (só encerra se a janela realmente estourou).</summary>
    public async Task ClaimAbandonment(int matchId)
    {
        var r = await arena.ResolveAbandonmentAsync(matchId, DateTime.UtcNow);
        if (r.Resolved)
            await Clients.Group(Group(matchId)).SendAsync("MatchFinished", await arena.GetMatchAsync(matchId));
    }

    /// <summary>Ao cair, inicia a janela de 30s pro jogador voltar; avisa o oponente
    /// pra ele mostrar o relógio e, ao fim, reivindicar a vitória por abandono.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("matchId", out var mv) && mv is int matchId)
        {
            await arena.MarkDisconnectedAsync(matchId, UserId, DateTime.UtcNow);
            await Clients.OthersInGroup(Group(matchId)).SendAsync("OpponentLeft", new { matchId, secondsToReturn = 30 });
        }
        await base.OnDisconnectedAsync(exception);
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

        await PushResolvedAsync(matchId, roundIndex, r.MatchFinished);
    }

    /// <summary>Tempo da rodada esgotou: quem não respondeu "pula" (0 pts) e a
    /// rodada avança — garante que ninguém fique travado (inclusive se o
    /// oponente caiu). Idempotente no servidor; os dois clientes podem chamar.</summary>
    public async Task TimeUp(int matchId, int roundIndex)
    {
        var r = await arena.ResolveExpiredRoundAsync(matchId, roundIndex, DateTime.UtcNow);
        if (!r.Resolved) return;
        await PushResolvedAsync(matchId, roundIndex, r.MatchFinished);
    }

    /// <summary>Revela o resultado da rodada e empurra a próxima (ou o fim).</summary>
    private async Task PushResolvedAsync(int matchId, int roundIndex, bool matchFinished)
    {
        var result = await arena.RoundResultAsync(matchId, roundIndex);
        await Clients.Group(Group(matchId)).SendAsync("RoundResult", result);

        if (matchFinished)
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
