using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.LiveQuiz.Ports;

namespace Unravel.API.Controllers;

/// <summary>
/// UC30 — Relatório pedagógico da turma. Endpoints do professor (Moderator):
/// listar as próprias sessões de Quiz ao Vivo e abrir o relatório de uma delas
/// (acertos por questão, lacunas por tópico, desempenho individual/agregado).
/// A autorização fina (ser o host da sessão) fica no serviço.
/// </summary>
[ApiController]
[Route("api/live-quiz/reports")]
[Authorize(Roles = "Moderator")]
public class LiveQuizReportController(ILiveQuizReportService reports) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/live-quiz/reports/sessions → sessões do professor (pra seleção)
    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions(CancellationToken ct)
        => Ok(await reports.ListHostSessionsAsync(UserId, ct));

    // GET /api/live-quiz/reports/{sessionId}
    [HttpGet("{sessionId:int}")]
    public async Task<IActionResult> GetReport(int sessionId, CancellationToken ct)
    {
        var report = await reports.GetReportAsync(UserId, sessionId, ct);
        return report is null
            ? NotFound(new { message = "Sessão não encontrada ou não pertence a você." })
            : Ok(report);
    }
}
