using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Unravel.Application.DTOs;
using Unravel.Application.UseCases;

namespace Unravel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(
    CreateUserUseCase createUserUseCase,
    GetUserUseCase getUserUseCase) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await createUserUseCase.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMe), new { }, user);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

        var user = await getUserUseCase.ExecuteAsync(userId, cancellationToken);
        return Ok(user);
    }
}
