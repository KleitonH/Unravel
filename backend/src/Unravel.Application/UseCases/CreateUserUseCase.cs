using Unravel.Application.DTOs;
using Unravel.Application.Gamification.Ports;
using Unravel.Application.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.Exceptions;
using Unravel.Domain.Ports;
using Unravel.Domain.ValueObjects;

namespace Unravel.Application.UseCases;

public class CreateUserUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IWelcomeGiftService welcomeGift,
    IModeratorInviteValidator inviteValidator)
{
    public async Task<UserResponse> ExecuteAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(request.Email);

        if (await userRepository.ExistsByEmailAsync(email, cancellationToken))
            throw new DomainException($"A user with email '{request.Email}' already exists.");

        // Cadastro como Educador (Moderator) exige código de convite válido —
        // barra escalonamento de privilégio. Sem "moderator" pedido, é aluno.
        var wantsModerator = string.Equals(request.Role?.Trim(), "moderator", StringComparison.OrdinalIgnoreCase);
        if (wantsModerator && !inviteValidator.IsValid(request.InviteCode))
            throw new DomainException("Código de convite de educador inválido.");

        var passwordHash = passwordHasher.Hash(request.Password);
        var user = User.Create(request.Name, email, passwordHash);
        if (wantsModerator) user.Role = Role.Moderator;

        await userRepository.AddAsync(user, cancellationToken);

        // Brinde de pré-registro: set "Mestre dos Gatos" + notificação de bônus.
        // Best-effort (o serviço engole exceções) — nunca quebra o cadastro.
        await welcomeGift.GrantAsync(user.Id, cancellationToken);

        return MapToResponse(user);
    }

    private static UserResponse MapToResponse(User user) =>
        new(user.Id, user.Name, user.Email.Value, user.IsActive, user.CreatedAt);
}
