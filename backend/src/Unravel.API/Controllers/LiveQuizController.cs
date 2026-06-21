using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.LiveQuiz.Ports;
using Unravel.Domain.Entities;

namespace Unravel.API.Controllers;

public record CreateLiveQuizBody(
    string      Mode,                 // "Turma" | "Livre"
    int         SecondsPerQuestion,
    bool        ShowRankBetween,
    bool        ShuffleQuestions,
    bool        ShuffleOptions,
    List<int>   QuestionChallengeIds,
    List<Guid>? AllowedUserIds);

public record SubmitLiveAnswerBody(int QuestionOrderIndex, int SelectedIndex);

/// <summary>
/// Quiz ao Vivo — controle REST da sessão. O host (professor/Moderator) cria
/// e controla; participantes entram por código e respondem. O push em tempo
/// real (SignalR) vem por cima deste mesmo serviço numa etapa seguinte.
/// </summary>
[ApiController]
[Route("api/live-quiz")]
[Authorize]
public class LiveQuizController(ILiveQuizService live) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string DisplayName =>
        User.FindFirstValue(JwtRegisteredClaimNames.Name)
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? "Participante";

    // ── Host (Moderator) ──────────────────────────────────────────────

    [HttpPost]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Create([FromBody] CreateLiveQuizBody body, CancellationToken ct)
    {
        if (body.QuestionChallengeIds is null || body.QuestionChallengeIds.Count == 0)
            return BadRequest(new { message = "Selecione ao menos uma pergunta." });

        var mode = body.Mode?.Equals("Livre", StringComparison.OrdinalIgnoreCase) == true
            ? LiveQuizMode.Livre : LiveQuizMode.Turma;

        var dto = await live.CreateAsync(UserId, new CreateLiveQuizRequest(
            mode, body.SecondsPerQuestion, body.ShowRankBetween, body.ShuffleQuestions, body.ShuffleOptions,
            body.QuestionChallengeIds, body.AllowedUserIds ?? new()), ct);

        return StatusCode(201, dto);
    }

    [HttpPost("{id:int}/start")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
        => await live.StartAsync(id, UserId, ct)
            ? Ok(await live.CurrentQuestionAsync(id, ct))
            : StatusCode(403, new { message = "Não autorizado ou sessão não está no lobby." });

    [HttpPost("{id:int}/advance")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Advance(int id, CancellationToken ct)
    {
        var idx = await live.AdvanceAsync(id, UserId, ct);
        if (idx < 0)
            return Ok(new { finished = true, leaderboard = await live.LeaderboardAsync(id, ct) });
        return Ok(new { finished = false, question = await live.CurrentQuestionAsync(id, ct) });
    }

    [HttpPost("{id:int}/finish")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Finish(int id, CancellationToken ct)
    {
        await live.FinishAsync(id, UserId, ct);
        return Ok(new { leaderboard = await live.LeaderboardAsync(id, ct) });
    }

    // ── Comum / participante ──────────────────────────────────────────

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var s = await live.GetAsync(id, ct);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var s = await live.GetByCodeAsync(code, ct);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpPost("join/{code}")]
    public async Task<IActionResult> Join(string code, CancellationToken ct)
    {
        var r = await live.JoinAsync(code, UserId, DisplayName, ct);
        return r.Outcome switch
        {
            JoinOutcome.Ok         => Ok(new { participant = r.Participant, session = r.Session }),
            JoinOutcome.NotFound   => NotFound(new { message = "Sala não encontrada." }),
            JoinOutcome.NotAllowed => StatusCode(403, new { message = "Você não está na lista de participantes desta turma." }),
            JoinOutcome.Finished   => Conflict(new { message = "Esta sessão já foi encerrada." }),
            _                      => BadRequest(),
        };
    }

    [HttpGet("{id:int}/current-question")]
    public async Task<IActionResult> CurrentQuestion(int id, CancellationToken ct)
    {
        var q = await live.CurrentQuestionAsync(id, ct);
        return q is null ? NoContent() : Ok(q);
    }

    [HttpPost("{id:int}/answer")]
    public async Task<IActionResult> Answer(int id, [FromBody] SubmitLiveAnswerBody body, CancellationToken ct)
    {
        var r = await live.SubmitAnswerAsync(id, UserId, body.QuestionOrderIndex, body.SelectedIndex, DateTime.UtcNow, ct);
        return Ok(r);
    }

    [HttpGet("{id:int}/leaderboard")]
    public async Task<IActionResult> Leaderboard(int id, CancellationToken ct)
        => Ok(await live.LeaderboardAsync(id, ct));

    /// <summary>Sessões de turma ativas em que o aluno pode entrar (banner "Minhas turmas").</summary>
    [HttpGet("active-for-me")]
    public async Task<IActionResult> ActiveForMe(CancellationToken ct)
        => Ok(await live.ActiveForUserAsync(UserId, ct));

    [HttpGet("{id:int}/question/{orderIndex:int}/result")]
    public async Task<IActionResult> QuestionResult(int id, int orderIndex, CancellationToken ct)
    {
        var r = await live.QuestionResultAsync(id, orderIndex, ct);
        return r is null ? NotFound() : Ok(r);
    }
}
