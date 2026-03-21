using Microsoft.AspNetCore.Mvc;
using Unravel.Application.DTOs;
using Unravel.Application.UseCases;
using Unravel.Domain.Exceptions;

namespace Unravel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthenticateUserUseCase authenticateUseCase) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticateUseCase.ExecuteAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticateUseCase.RefreshAsync(request.RefreshToken, cancellationToken);
        return Ok(result);
    }
}
