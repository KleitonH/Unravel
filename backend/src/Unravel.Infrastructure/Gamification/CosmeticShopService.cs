using Microsoft.EntityFrameworkCore;
using Unravel.Application.Gamification.Ports;
using Unravel.Domain.Entities;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Gamification;

/// <summary>
/// PR 63 — impl da loja cosmética. Acessa o <see cref="ApplicationDbContext"/>
/// diretamente (padrão dos services testáveis via EF InMemory, como o
/// ContentChaptersService). Toda a lógica de posse/saldo/equip vive aqui.
/// </summary>
public sealed class CosmeticShopService(ApplicationDbContext db) : ICosmeticShopService
{
    public async Task<ShopCatalogDto> GetCatalogAsync(Guid userId, CancellationToken ct = default)
    {
        var cosmetics = await db.NaviCosmetic.AsNoTracking()
            .OrderBy(c => c.Type).ThenBy(c => c.Rarity).ThenBy(c => c.Id)
            .ToListAsync(ct);

        var owned = await db.UserCosmetic.AsNoTracking()
            .Where(uc => uc.UserId == userId)
            .ToDictionaryAsync(uc => uc.CosmeticId, uc => uc.IsEquipped, ct);

        var user = await db.User.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Coins, u.Stars })
            .FirstOrDefaultAsync(ct);

        var items = cosmetics.Select(c =>
        {
            var (category, slot) = MapType(c.Type);
            var (currency, price) = PriceOf(c);
            return new ShopItemDto(
                Id:           c.Id,
                Name:         c.Name,
                Description:  c.Description,
                Category:     category,
                Slot:         slot,
                AssetSlug:    c.AssetSlug,
                Rarity:       c.Rarity.ToString().ToLowerInvariant(),
                Currency:     currency,
                Price:        price,
                Owned:        owned.ContainsKey(c.Id),
                Equipped:     owned.TryGetValue(c.Id, out var eq) && eq,
                LockedReason: c.LockedReason);
        }).ToList();

        return new ShopCatalogDto(items, new ShopBalance(user?.Coins ?? 0, user?.Stars ?? 0));
    }

    public async Task<BuyResultDto> BuyAsync(Guid userId, int cosmeticId, CancellationToken ct = default)
    {
        var cosmetic = await db.NaviCosmetic.FindAsync(new object[] { cosmeticId }, ct);
        if (cosmetic is null)
            return new BuyResultDto(BuyOutcome.NotFound, cosmeticId, null, null, null);

        if (!string.IsNullOrWhiteSpace(cosmetic.LockedReason))
            return new BuyResultDto(BuyOutcome.Locked, cosmeticId, null, null, null);

        var (currency, price) = PriceOf(cosmetic);
        if (currency is null || price is null or <= 0)
            return new BuyResultDto(BuyOutcome.Locked, cosmeticId, null, null, null);

        var already = await db.UserCosmetic
            .AnyAsync(uc => uc.UserId == userId && uc.CosmeticId == cosmeticId, ct);
        if (already)
            return new BuyResultDto(BuyOutcome.AlreadyOwned, cosmeticId, null, null, null);

        var user = await db.User.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return new BuyResultDto(BuyOutcome.NotFound, cosmeticId, null, null, null);

        var balance = currency == "stars" ? user.Stars : user.Coins;
        if (balance < price.Value)
            return new BuyResultDto(BuyOutcome.InsufficientFunds, cosmeticId,
                new ShopBalance(user.Coins, user.Stars), currency, price);

        // Debita.
        if (currency == "stars") user.Stars -= price.Value;
        else                     user.Coins -= price.Value;

        // Desequipa os do mesmo slot e cria o novo já equipado (espelha o
        // auto-equip do protótipo na compra).
        var sameSlot = await db.UserCosmetic
            .Include(uc => uc.Cosmetic)
            .Where(uc => uc.UserId == userId && uc.Cosmetic.Type == cosmetic.Type && uc.IsEquipped)
            .ToListAsync(ct);
        sameSlot.ForEach(uc => uc.IsEquipped = false);

        db.UserCosmetic.Add(new UserCosmetic
        {
            UserId = userId, CosmeticId = cosmeticId, IsEquipped = true, AcquiredAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return new BuyResultDto(BuyOutcome.Ok, cosmeticId,
            new ShopBalance(user.Coins, user.Stars), currency, price);
    }

    public async Task<bool> SetEquippedAsync(Guid userId, int cosmeticId, bool equipped, CancellationToken ct = default)
    {
        var userCosmetic = await db.UserCosmetic
            .Include(uc => uc.Cosmetic)
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CosmeticId == cosmeticId, ct);
        if (userCosmetic is null) return false;

        if (equipped)
        {
            var sameSlot = await db.UserCosmetic
                .Include(uc => uc.Cosmetic)
                .Where(uc => uc.UserId == userId
                          && uc.Cosmetic.Type == userCosmetic.Cosmetic.Type
                          && uc.IsEquipped)
                .ToListAsync(ct);
            sameSlot.ForEach(uc => uc.IsEquipped = false);
        }

        userCosmetic.IsEquipped = equipped;
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ── helpers ──────────────────────────────────────────────────────

    /// <summary>CosmeticType → (categoria de exibição, slot do NAVI).</summary>
    private static (string category, string slot) MapType(CosmeticType type) => type switch
    {
        CosmeticType.Hat        => ("chapeu",    "hat"),
        CosmeticType.Accessory  => ("acessorio", "accessory"),
        CosmeticType.Fur        => ("pelagem",   "fur"),
        CosmeticType.Expression => ("expressao", "mood"),
        _                       => (type.ToString().ToLowerInvariant(), type.ToString().ToLowerInvariant()),
    };

    /// <summary>Stars têm precedência se &gt; 0; senão coins; senão não-comprável.</summary>
    private static (string? currency, int? price) PriceOf(NaviCosmetic c)
        => c.StarPrice > 0 ? ("stars", c.StarPrice)
         : c.CoinPrice > 0 ? ("coins", c.CoinPrice)
         : (null, null);
}
