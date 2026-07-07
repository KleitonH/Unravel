using Microsoft.Extensions.Configuration;
using Unravel.Application.Ports;

namespace Unravel.Infrastructure.Auth;

/// <summary>
/// Compara o código informado com o segredo <c>Moderator:InviteCode</c> da
/// configuração. Se nenhum código estiver configurado, NINGUÉM vira
/// moderador pelo cadastro (fail-closed) — moderadores teriam de ser
/// provisionados manualmente.
/// </summary>
public sealed class ModeratorInviteValidator(IConfiguration config) : IModeratorInviteValidator
{
    public bool IsValid(string? code)
    {
        var expected = config["Moderator:InviteCode"];
        if (string.IsNullOrWhiteSpace(expected)) return false;   // fail-closed
        return !string.IsNullOrWhiteSpace(code)
            && string.Equals(code.Trim(), expected.Trim(), StringComparison.Ordinal);
    }
}
