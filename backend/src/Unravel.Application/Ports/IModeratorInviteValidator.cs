namespace Unravel.Application.Ports;

/// <summary>
/// Valida o código de convite exigido para se cadastrar como Educador
/// (Moderator). Impede escalonamento de privilégio: sem um código válido,
/// o cadastro de moderador é recusado. O código correto é um segredo de
/// configuração da instância, distribuído pela instituição.
/// </summary>
public interface IModeratorInviteValidator
{
    bool IsValid(string? code);
}
