using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Services;

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
