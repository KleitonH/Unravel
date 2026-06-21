using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Classes.Ports;

namespace Unravel.API.Controllers;

public record CreateTurmaBody(string Name, string? Description, string? Emblem);
public record InviteTurmaBody(Guid StudentId);

/// <summary>
/// Turmas — vínculo professor↔aluno. Endpoints de professor exigem o papel
/// Moderator; os de aluno são abertos a qualquer autenticado. A autorização
/// fina (ser dono da turma / dono do convite) fica no serviço.
/// </summary>
[ApiController]
[Route("api/turmas")]
[Authorize]
public class TurmasController(ITurmaService turmas) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Professor (Moderator) ─────────────────────────────────────────

    // POST /api/turmas
    [HttpPost]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Create([FromBody] CreateTurmaBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name) || body.Name.Trim().Length < 2)
            return BadRequest(new { message = "Dê um nome com pelo menos 2 caracteres à turma." });
        var dto = await turmas.CreateAsync(UserId, body.Name, body.Description, body.Emblem, ct);
        return StatusCode(201, dto);
    }

    // GET /api/turmas/owned
    [HttpGet("owned")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> GetOwned(CancellationToken ct)
        => Ok(await turmas.GetOwnedAsync(UserId, ct));

    // GET /api/turmas/{id}
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> GetDetail(int id, CancellationToken ct)
    {
        var dto = await turmas.GetDetailAsync(UserId, id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // GET /api/turmas/{id}/search-students?q=&take=
    [HttpGet("{id:int}/search-students")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> SearchStudents(int id, [FromQuery] string q, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await turmas.SearchStudentsAsync(UserId, id, q, take, ct));

    // POST /api/turmas/{id}/invite { studentId }
    [HttpPost("{id:int}/invite")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Invite(int id, [FromBody] InviteTurmaBody body, CancellationToken ct)
    {
        var r = await turmas.InviteAsync(UserId, id, body.StudentId, ct);
        return r.Outcome switch
        {
            TurmaActionOutcome.Ok             => StatusCode(201, new { memberId = r.Id }),
            TurmaActionOutcome.NotFound       => NotFound(new { message = "Turma ou aluno não encontrado." }),
            TurmaActionOutcome.NotAuthorized  => StatusCode(403, new { message = "Você não é dono dessa turma." }),
            TurmaActionOutcome.AlreadyMember  => Conflict(new { message = "Esse aluno já está na turma." }),
            TurmaActionOutcome.AlreadyInvited => Conflict(new { message = "Esse aluno já foi convidado." }),
            TurmaActionOutcome.NotAStudent    => BadRequest(new { message = "Só é possível convidar alunos." }),
            _                                 => BadRequest(),
        };
    }

    // DELETE /api/turmas/{id}/members/{studentId}
    [HttpDelete("{id:int}/members/{studentId:guid}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> RemoveMember(int id, Guid studentId, CancellationToken ct)
    {
        var r = await turmas.RemoveMemberAsync(UserId, id, studentId, ct);
        return r.Outcome switch
        {
            TurmaActionOutcome.Ok            => NoContent(),
            TurmaActionOutcome.NotAuthorized => StatusCode(403, new { message = "Você não é dono dessa turma." }),
            _                                => NotFound(new { message = "Membro não encontrado." }),
        };
    }

    // DELETE /api/turmas/{id}  (arquiva)
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        var r = await turmas.ArchiveAsync(UserId, id, ct);
        return r.Outcome switch
        {
            TurmaActionOutcome.Ok            => NoContent(),
            TurmaActionOutcome.NotAuthorized => StatusCode(403, new { message = "Você não é dono dessa turma." }),
            _                                => NotFound(new { message = "Turma não encontrada." }),
        };
    }

    // ── Aluno ─────────────────────────────────────────────────────────

    // GET /api/turmas/mine
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => Ok(await turmas.GetMineAsync(UserId, ct));

    // GET /api/turmas/invites
    [HttpGet("invites")]
    public async Task<IActionResult> GetInvites(CancellationToken ct)
        => Ok(await turmas.GetInvitesAsync(UserId, ct));

    // PUT /api/turmas/invites/{memberId}/accept
    [HttpPut("invites/{memberId:int}/accept")]
    public async Task<IActionResult> Accept(int memberId, CancellationToken ct)
        => Respond(await turmas.RespondInviteAsync(UserId, memberId, accept: true, ct));

    // PUT /api/turmas/invites/{memberId}/decline
    [HttpPut("invites/{memberId:int}/decline")]
    public async Task<IActionResult> Decline(int memberId, CancellationToken ct)
        => Respond(await turmas.RespondInviteAsync(UserId, memberId, accept: false, ct));

    // DELETE /api/turmas/{id}/leave
    [HttpDelete("{id:int}/leave")]
    public async Task<IActionResult> Leave(int id, CancellationToken ct)
    {
        var r = await turmas.LeaveAsync(UserId, id, ct);
        return r.Outcome == TurmaActionOutcome.Ok
            ? NoContent()
            : NotFound(new { message = "Você não participa dessa turma." });
    }

    private IActionResult Respond(TurmaActionResult r) => r.Outcome switch
    {
        TurmaActionOutcome.Ok            => Ok(new { memberId = r.Id }),
        TurmaActionOutcome.NotFound      => NotFound(new { message = "Convite não encontrado." }),
        TurmaActionOutcome.NotAuthorized => StatusCode(403, new { message = "Convite indisponível para responder." }),
        _                                => BadRequest(),
    };
}
