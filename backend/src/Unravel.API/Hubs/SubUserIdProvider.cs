using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Unravel.API.Hubs;

/// <summary>
/// Mapeia a identidade do SignalR pelo claim <c>sub</c> do JWT (o id do
/// usuário), com fallback pra <c>NameIdentifier</c>. Necessário pra
/// <c>Clients.User(userId)</c> entregar pushs direcionados — ex.: avisar quem
/// está na fila da Arena que o pareamento aconteceu.
/// </summary>
public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
