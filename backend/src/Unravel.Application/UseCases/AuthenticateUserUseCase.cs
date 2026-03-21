using Unravel.Application.DTOs;
using Unravel.Application.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Exceptions;
using Unravel.Domain.Ports;
using Unravel.Domain.ValueObjects;

namespace Unravel.Application.UseCases;

public class AuthenticateUserUseCase(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
{
    private const int RefreshTokenExpirationDays = 7;

    public async Task<AuthResponse> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(request.Email);
        var user = await userRepository.GetByEmailAsync(email, cancellationToken)
            ?? throw new DomainException("Invalid credentials.");

        if (!user.IsActive)
            throw new DomainException("User account is inactive.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("Invalid credentials.");

        return await GenerateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await refreshTokenRepository.GetByTokenAsync(token, cancellationToken)
            ?? throw new DomainException("Invalid refresh token.");

        if (!refreshToken.IsActive)
            throw new DomainException("Refresh token is expired or revoked.");

        var user = await userRepository.GetByIdAsync(refreshToken.UserId, cancellationToken)
            ?? throw new DomainException("User not found.");

        refreshToken.Revoke();
        await refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);

        return await GenerateAuthResponseAsync(user, cancellationToken);
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays);

        var refreshToken = RefreshToken.Create(user.Id, rawRefreshToken, expiresAt);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        var userResponse = new UserResponse(user.Id, user.Name, user.Email.Value, user.IsActive, user.CreatedAt);

        return new AuthResponse(accessToken, rawRefreshToken, expiresAt, userResponse);
    }
}
