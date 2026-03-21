using Unravel.Application.DTOs;
using Unravel.Domain.Exceptions;
using Unravel.Domain.Ports;

namespace Unravel.Application.UseCases;

public class GetUserUseCase(IUserRepository userRepository)
{
    public async Task<UserResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new DomainException($"User '{userId}' not found.");

        return new UserResponse(user.Id, user.Name, user.Email.Value, user.IsActive, user.CreatedAt);
    }
}
