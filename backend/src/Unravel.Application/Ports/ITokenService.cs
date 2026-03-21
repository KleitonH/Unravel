using Unravel.Domain.Entities;

namespace Unravel.Application.Ports;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Guid? GetUserIdFromToken(string token);
}
