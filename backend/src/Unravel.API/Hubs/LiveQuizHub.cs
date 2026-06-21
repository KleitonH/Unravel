using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Unravel.Application.LiveQuiz.Ports;

namespace Unravel.API.Hubs;

/// <summary>
/// Hub do Quiz ao Vivo (<c>/hubs/live-quiz</c>, JWT via header ou
/// <c>?access_token=</c>). Fino: toda a lógica/estado fica no
/// <see cref="ILiveQuizService"/>; o hub só roteia chamadas e faz o push
/// pro grupo da sessão (<c>live:{sessionId}</c>).
///
/// <para><b>Métodos no cliente</b> (contrato): <c>Session</c>, <c>Leaderboard</c>,
/// <c>Joined</c>, <c>JoinError</c>, <c>ParticipantJoined</c>, <c>QuestionStarted</c>,
/// <c>AnswerResult</c>, <c>AnswerTally</c>, <c>QuestionEnded</c>,
/// <c>SessionEnded</c>, <c>ControlError</c>.</para>
/// </summary>
[Authorize]
public sealed class LiveQuizHub(ILiveQuizService live) : Hub
{
    public static string Group(int sessionId) => $"live:{sessionId}";

    private Guid UserId => Guid.Parse(
        Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string DisplayName =>
        Context.User?.FindFirstValue(JwtRegisteredClaimNames.Name)
        ?? Context.User?.FindFirstValue(ClaimTypes.Name)
        ?? "Participante";

    // ── Host (sala já criada via REST) ────────────────────────────────

    public async Task HostJoin(int sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(sessionId));
        await Clients.Caller.SendAsync("Session", await live.GetAsync(sessionId));
        await Clients.Caller.SendAsync("Leaderboard", await live.LeaderboardAsync(sessionId));
    }

    public async Task StartSession(int sessionId)
    {
        if (!await live.StartAsync(sessionId, UserId))
        {
            await Clients.Caller.SendAsync("ControlError", "start");
            return;
        }
        await Clients.Group(Group(sessionId)).SendAsync("QuestionStarted", await live.CurrentQuestionAsync(sessionId));
    }

    /// <summary>Revela gabarito + ranking da pergunta atual (fim da rodada).</summary>
    public async Task RevealQuestion(int sessionId, int orderIndex)
    {
        var result = await live.QuestionResultAsync(sessionId, orderIndex);
        var board  = await live.LeaderboardAsync(sessionId);
        await Clients.Group(Group(sessionId)).SendAsync("QuestionEnded", new { result, leaderboard = board });
    }

    public async Task NextQuestion(int sessionId)
    {
        var idx = await live.AdvanceAsync(sessionId, UserId);
        if (idx < 0)
            await Clients.Group(Group(sessionId)).SendAsync("SessionEnded", await live.LeaderboardAsync(sessionId));
        else
            await Clients.Group(Group(sessionId)).SendAsync("QuestionStarted", await live.CurrentQuestionAsync(sessionId));
    }

    public async Task EndSession(int sessionId)
    {
        await live.FinishAsync(sessionId, UserId);
        await Clients.Group(Group(sessionId)).SendAsync("SessionEnded", await live.LeaderboardAsync(sessionId));
    }

    // ── Participante ──────────────────────────────────────────────────

    public async Task JoinSession(string code)
    {
        var r = await live.JoinAsync(code, UserId, DisplayName);
        if (r.Outcome != JoinOutcome.Ok || r.Session is null || r.Participant is null)
        {
            await Clients.Caller.SendAsync("JoinError", r.Outcome.ToString());
            return;
        }

        var sid = r.Session.Id;
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(sid));
        await Clients.Caller.SendAsync("Joined", new { participant = r.Participant, session = r.Session });
        await Clients.Group(Group(sid)).SendAsync("ParticipantJoined",
            new { name = r.Participant.DisplayName, count = r.Session.ParticipantCount });

        // Entrou com a sessão já rodando → manda a pergunta atual pro retardatário.
        var q = await live.CurrentQuestionAsync(sid);
        if (q is not null) await Clients.Caller.SendAsync("QuestionStarted", q);
    }

    public async Task SubmitAnswer(int sessionId, int orderIndex, int optionIndex)
    {
        var r = await live.SubmitAnswerAsync(sessionId, UserId, orderIndex, optionIndex, DateTime.UtcNow);
        await Clients.Caller.SendAsync("AnswerResult", r);
        if (r.Accepted)
        {
            var count = await live.AnsweredCountAsync(sessionId, orderIndex);
            await Clients.Group(Group(sessionId)).SendAsync("AnswerTally", new { orderIndex, count });
        }
    }
}
