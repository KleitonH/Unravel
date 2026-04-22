using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.DTOs;
using Unravel.Application.Services;

namespace Unravel.API.Controllers;

[ApiController]
[Route("api/contents")]
[Authorize]
public class ContentsController : ControllerBase
{
    private readonly IContentService _contents;
    private Guid UserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public ContentsController(IContentService contents) => _contents = contents;

    [HttpGet]
    public async Task<IActionResult> GetByTrail([FromQuery] int trailId)
        => Ok(await _contents.GetByTrailAsync(trailId, UserId));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var content = await _contents.GetByIdAsync(id, UserId);
        return content is null ? NotFound() : Ok(content);
    }

    [HttpPost]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Create([FromBody] CreateContentRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _contents.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateContentRequest dto)
    {
        var updated = await _contents.UpdateAsync(id, dto, UserId);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Moderator")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _contents.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }
}
