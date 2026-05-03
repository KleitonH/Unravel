using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unravel.Application.DTOs;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.API.Controllers;

public record MetricsResponse(
    int  TotalStudents,
    int  TotalTrails,
    int  TotalContents,
    int  TotalChallenges,
    long TotalXpDistributed,
    int  TotalEnrollments,
    int  TotalCompletions
);

[ApiController]
[Route("api/moderator")]
[Authorize(Roles = "Moderator")]
public class ModeratorController(ApplicationDbContext db) : ControllerBase
{
    // ── Contents ─────────────────────────────────────────────────

    // GET /api/moderator/contents?trailId=
    [HttpGet("contents")]
    public async Task<IActionResult> GetContents([FromQuery] int? trailId)
    {
        var query = db.Content
            .Where(c => c.IsActive)
            .AsQueryable();

        if (trailId.HasValue)
            query = query.Where(c => c.TrailId == trailId.Value);

        var contents = await query
            .OrderBy(c => c.TrailId)
            .ThenBy(c => c.Order)
            .Select(c => new ContentResponse(
                c.Id, c.TrailId, c.Title, c.Body, c.ExternalUrl,
                c.Type.ToString(), c.Level.ToString(), c.Order,
                false
            ))
            .ToListAsync();

        return Ok(contents);
    }

    // POST /api/moderator/contents
    [HttpPost("contents")]
    public async Task<IActionResult> CreateContent([FromBody] CreateContentRequest dto)
    {
        var trail = await db.Trail.FindAsync(dto.TrailId);
        if (trail is null) return NotFound(new { message = "Trilha não encontrada." });

        var content = new Content
        {
            TrailId     = dto.TrailId,
            Title       = dto.Title,
            Body        = dto.Body,
            ExternalUrl = dto.ExternalUrl,
            Type        = (ContentType)dto.Type,
            Level       = (DifficultyLevel)dto.Level,
            Order       = dto.Order
        };

        db.Content.Add(content);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetContents), new { trailId = content.TrailId },
            new ContentResponse(
                content.Id, content.TrailId, content.Title, content.Body, content.ExternalUrl,
                content.Type.ToString(), content.Level.ToString(), content.Order, false
            ));
    }

    // PUT /api/moderator/contents/{id}
    [HttpPut("contents/{id:int}")]
    public async Task<IActionResult> UpdateContent(int id, [FromBody] UpdateContentRequest dto)
    {
        var content = await db.Content.FindAsync(id);
        if (content is null || !content.IsActive) return NotFound();

        if (dto.Title       is not null) content.Title       = dto.Title;
        if (dto.Body        is not null) content.Body        = dto.Body;
        if (dto.ExternalUrl is not null) content.ExternalUrl = dto.ExternalUrl;
        if (dto.Type        is not null) content.Type        = (ContentType)dto.Type;
        if (dto.Level       is not null) content.Level       = (DifficultyLevel)dto.Level;
        if (dto.Order       is not null) content.Order       = dto.Order.Value;

        await db.SaveChangesAsync();

        return Ok(new ContentResponse(
            content.Id, content.TrailId, content.Title, content.Body, content.ExternalUrl,
            content.Type.ToString(), content.Level.ToString(), content.Order, false
        ));
    }

    // DELETE /api/moderator/contents/{id}
    [HttpDelete("contents/{id:int}")]
    public async Task<IActionResult> DeleteContent(int id)
    {
        var content = await db.Content.FindAsync(id);
        if (content is null || !content.IsActive) return NotFound();

        content.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Metrics ──────────────────────────────────────────────────

    // GET /api/moderator/metrics
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = new MetricsResponse(
            TotalStudents:      await db.User.CountAsync(u => u.Role == Role.Student && u.IsActive),
            TotalTrails:        await db.Trail.CountAsync(t => t.IsActive),
            TotalContents:      await db.Content.CountAsync(c => c.IsActive),
            TotalChallenges:    await db.Challenge.CountAsync(c => c.IsActive),
            TotalXpDistributed: await db.User.SumAsync(u => (long)u.Xp),
            TotalEnrollments:   await db.UserTrail.CountAsync(),
            TotalCompletions:   await db.UserContent.CountAsync(uc => uc.IsCompleted)
        );

        return Ok(metrics);
    }
}
