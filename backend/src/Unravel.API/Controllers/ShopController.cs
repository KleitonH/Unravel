using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unravel.Application.Gamification.Ports;

namespace Unravel.API.Controllers;

/// <summary>
/// PR 63 — Loja cosmética ("Toca do NAVI"). Catálogo + compra (debita
/// coins/stars) + equipar/desequipar. Delega ao <see cref="ICosmeticShopService"/>.
/// </summary>
[ApiController]
[Route("api/shop")]
[Authorize]
public sealed class ShopController(ICosmeticShopService shop) : ControllerBase
{
    private Guid UserId => Guid.Parse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Catálogo com flags do usuário (owned/equipped) + saldo.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCatalog(CancellationToken ct)
        => Ok(await shop.GetCatalogAsync(UserId, ct));

    /// <summary>Compra um cosmético. 402 se saldo insuficiente; 409 se já
    /// possui; 400 se item de evento/sem preço; 404 se não existe.</summary>
    [HttpPost("{cosmeticId:int}/buy")]
    public async Task<IActionResult> Buy(int cosmeticId, CancellationToken ct)
    {
        var r = await shop.BuyAsync(UserId, cosmeticId, ct);
        return r.Outcome switch
        {
            BuyOutcome.Ok               => Ok(r),
            BuyOutcome.NotFound         => NotFound(new { message = "Cosmético não encontrado." }),
            BuyOutcome.AlreadyOwned     => Conflict(new { message = "Você já possui este item." }),
            BuyOutcome.Locked           => BadRequest(new { message = "Item de evento — não disponível para compra." }),
            BuyOutcome.InsufficientFunds => StatusCode(402, new {
                message = "Saldo insuficiente — continue estudando!",
                currency = r.Currency, price = r.Price, balance = r.Balance,
            }),
            _ => StatusCode(500, new { message = "Erro inesperado." }),
        };
    }

    /// <summary>Equipa o cosmético (1 por slot).</summary>
    [HttpPut("{cosmeticId:int}/equip")]
    public async Task<IActionResult> Equip(int cosmeticId, CancellationToken ct)
        => await shop.SetEquippedAsync(UserId, cosmeticId, true, ct)
            ? Ok(new { message = "Equipado." })
            : NotFound(new { message = "Você não possui este item." });

    /// <summary>Desequipa o cosmético.</summary>
    [HttpPut("{cosmeticId:int}/unequip")]
    public async Task<IActionResult> Unequip(int cosmeticId, CancellationToken ct)
        => await shop.SetEquippedAsync(UserId, cosmeticId, false, ct)
            ? Ok(new { message = "Desequipado." })
            : NotFound(new { message = "Você não possui este item." });
}
