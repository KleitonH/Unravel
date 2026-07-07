namespace Unravel.Application.Gamification.Ports;

/// <summary>
/// Concede o brinde de boas-vindas a um usuário recém-cadastrado: o set
/// cosmético "Mestre dos Gatos" (já equipado) + uma notificação de bônus.
///
/// <para><b>Best-effort</b>: a implementação nunca lança — se algo falhar, o
/// cadastro segue normalmente; o aluno simplesmente não recebe o brinde
/// (e pode ser reconcedido depois). Chamado pelo <c>CreateUserUseCase</c>
/// após o usuário ser persistido.</para>
/// </summary>
public interface IWelcomeGiftService
{
    Task GrantAsync(Guid userId, CancellationToken ct = default);
}
