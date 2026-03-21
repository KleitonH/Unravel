using Unravel.Application.DTOs;
using Unravel.Application.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Exceptions;
using Unravel.Domain.Ports;
using Unravel.Domain.ValueObjects;

namespace Unravel.Application.UseCases;

public class CreateUserUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher)
{
    public async Task<UserResponse> ExecuteAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(request.Email);

        if (await userRepository.ExistsByEmailAsync(email, cancellationToken))
            throw new DomainException($"A user with email '{request.Email}' already exists.");

        var passwordHash = passwordHasher.Hash(request.Password);
        var user = User.Create(request.Name, email, passwordHash);

        await userRepository.AddAsync(user, cancellationToken);

        return MapToResponse(user);
    }

    private static UserResponse MapToResponse(User user) =>
        new(user.Id, user.Name, user.Email.Value, user.IsActive, user.CreatedAt);
}
