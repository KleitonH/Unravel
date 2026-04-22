using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.DTOs;
using Unravel.Application.Services;

namespace Unravel.API.Controllers;

[ApiController]
[Route("api/trails")]
[Authorize]
public class TrailsController : ControllerBase
{
    private readonly ITrailService _trails;
    private Guid UserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public TrailsController(ITrailService trails) => _trails = trails;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _trails.GetAllAsync(UserId));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var trail = await _trails.GetByIdAsync(id, UserId);
        return trail is null ? NotFound() : Ok(trail);
    }

    [HttpPost]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Create([FromBody] CreateTrailRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _trails.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTrailRequest dto)
    {
        var updated = await _trails.UpdateAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _trails.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("{id:int}/sequence")]
    public async Task<IActionResult> GetSequence(int id)
        => Ok(await _trails.GetSequenceAsync(UserId, id));

    [HttpPost("{id:int}/enroll")]
    public async Task<IActionResult> Enroll(int id)
        => Ok(await _trails.EnrollAsync(UserId, id));

    [HttpGet("{id:int}/progress")]
    public async Task<IActionResult> GetProgress(int id)
    {
        var progress = await _trails.GetProgressAsync(UserId, id);
        return progress is null ? NotFound() : Ok(progress);
    }

    [HttpPost("complete-content")]
    public async Task<IActionResult> CompleteContent([FromBody] CompleteContentRequest dto)
    {
        try
        {
            var progress = await _trails.CompleteContentAsync(UserId, dto.ContentId);
            return Ok(progress);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
