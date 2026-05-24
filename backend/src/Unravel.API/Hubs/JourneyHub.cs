using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Unravel.API.Hubs;

/// <summary>
/// Hub de notificações do Journey. Cliente conecta em <c>/hubs/journey</c>
/// (autenticado por JWT, via header <i>ou</i> query string
/// <c>?access_token=</c>) e é automaticamente colocado no grupo
/// <c>user:{userId}</c>. Eventos publicados pelo <c>SignalRJourneyEventBus</c>
/// chegam só ao usuário dono do evento.
///
/// <para><b>Métodos invocados no cliente</b> (nomes estáveis, parte do
/// contrato):</para>
/// <list type="bullet">
///   <item><c>DailyPlanGenerated(payload)</c> — disparado pelo cron diário.</item>
///   <item><c>StreakReset(payload)</c> — disparado pelo cron quando inatividade ≥ 2 dias.</item>
/// </list>
///
/// <para>Não exigimos que o cliente invoque nada — o hub é puramente push.
/// Mantemos a classe enxuta intencionalmente; lógica fica no event bus.</para>
/// </summary>
[Authorize]
public sealed class JourneyHub : Hub
{
    /// <summary>Padrão dos nomes de grupo por usuário. Centralizado aqui
    /// para que o <c>SignalRJourneyEventBus</c> use a mesma convenção.</summary>
    public static string UserGroup(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = ExtractUserId(Context.User);
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // SignalR limpa automaticamente as group memberships ao desconectar,
        // mas chamamos Remove explicitamente quando o caso é distintivo
        // (logout antes de desconectar, troca de identidade etc).
        var userId = ExtractUserId(Context.User);
        if (userId is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId.Value));

        await base.OnDisconnectedAsync(exception);
    }

    private static Guid? ExtractUserId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
