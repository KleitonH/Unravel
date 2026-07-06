using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.Services;
using Unravel.Domain.Forge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.API.Controllers;

[ApiController]
[Route("api/challenges")]
[Authorize]
public class ChallengesController(IChallengeService challenges) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/challenges?trailId=1
    [HttpGet]
    public async Task<IActionResult> GetByTrail([FromQuery] int trailId)
        => Ok(await challenges.GetByTrailAsync(trailId));

    // GET /api/challenges/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var challenge = await challenges.GetByIdAsync(id);
        return challenge is null ? NotFound() : Ok(challenge);
    }

    // GET /api/challenges/daily
    [HttpGet("daily")]
    public async Task<IActionResult> GetDailyStatus()
        => Ok(await challenges.GetDailyStatusAsync(UserId));

    // POST /api/challenges/submit
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitChallengeRequest dto)
    {
        try
        {
            return Ok(await challenges.SubmitAsync(UserId, dto));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // POST /api/challenges/{id}/feedback  — aluno sinaliza pergunta inadequada
    //
    // A "bandeirinha" do quiz. O aluno escolhe o tipo do problema (gabarito
    // errado / ambígua / múltipla correta / fora do conteúdo / outro) e,
    // opcionalmente, comenta. Fica em fila (Aberto) pro moderador triar.
    //
    // Idempotente por (pergunta, aluno): se já sinalizou e ainda está aberto,
    // atualiza o tipo/comentário (aluno mudou de ideia). Se já foi triado,
    // bloqueia novo report (409) — o histórico daquele aluno é preservado.
    [HttpPost("{id:int}/feedback")]
    public async Task<IActionResult> SubmitFeedback(
        int id,
        [FromBody] SubmitFeedbackRequest dto,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(FeedbackReason), dto.Reason))
            return BadRequest(new { message = "Tipo de problema inválido." });
        if (dto.Reason == FeedbackReason.Outro && string.IsNullOrWhiteSpace(dto.Comment))
            return BadRequest(new { message = "Descreva o problema no comentário." });

        var challenge = await db.GeneratedChallenge.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new { g.Id, g.ContentId })
            .FirstOrDefaultAsync(ct);
        if (challenge is null) return NotFound(new { message = "Pergunta não encontrada." });

        var comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();
        var userId  = UserId;

        var existing = await db.ChallengeFeedback
            .FirstOrDefaultAsync(f => f.GeneratedChallengeId == id && f.UserId == userId, ct);
        if (existing is not null)
        {
            if (existing.Status != FeedbackStatus.Aberto)
                return Conflict(new { message = "Você já sinalizou esta pergunta." });

            existing.Reason    = dto.Reason;
            existing.Comment   = comment;
            existing.CreatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Ok(new { existing.Id, updated = true });
        }

        var feedback = new ChallengeFeedback
        {
            GeneratedChallengeId = id,
            ContentId            = challenge.ContentId,
            UserId               = userId,
            Reason               = dto.Reason,
            Comment              = comment,
            Status               = FeedbackStatus.Aberto,
            CreatedAt            = DateTime.UtcNow,
        };
        db.ChallengeFeedback.Add(feedback);
        await db.SaveChangesAsync(ct);
        return Ok(new { feedback.Id, updated = false });
    }

    // POST /api/challenges  [Moderator]
    [HttpPost]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Create([FromBody] CreateChallengeRequest dto)
    {
        var created = await challenges.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // DELETE /api/challenges/{id}  [Moderator]
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await challenges.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }
}

/// <summary>Payload da bandeirinha do quiz. <c>Reason</c> é o
/// <see cref="FeedbackReason"/> (0–4); <c>Comment</c> é obrigatório só
/// quando Reason = Outro (4).</summary>
public record SubmitFeedbackRequest(FeedbackReason Reason, string? Comment);
