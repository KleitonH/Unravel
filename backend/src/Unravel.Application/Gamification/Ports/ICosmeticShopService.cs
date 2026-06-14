namespace Unravel.Application.Gamification.Ports;

/// <summary>
/// PR 63 — Loja cosmética ("Toca do NAVI"). Catálogo de cosméticos do
/// mascote + compra (debita coins/stars do aluno) + equipar/desequipar.
/// Impl na Infrastructure (acessa o DbContext); testável via EF InMemory.
///
/// <para>Economia: <b>coins</b> (comum, ganho estudando) e <b>stars</b>
/// (rara). Um item custa numa das duas moedas. É distinto da "lã" do
/// moderador (geração de perguntas).</para>
/// </summary>
public interface ICosmeticShopService
{
    /// <summary>Catálogo completo com flags do usuário (owned/equipped) + saldo.</summary>
    Task<ShopCatalogDto> GetCatalogAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Compra um cosmético: valida posse/lock/saldo, debita a moeda,
    /// cria o <c>UserCosmetic</c> já equipado (desequipando o do mesmo slot).</summary>
    Task<BuyResultDto> BuyAsync(Guid userId, int cosmeticId, CancellationToken ct = default);

    /// <summary>Equipa (1 por slot) ou desequipa um cosmético possuído.
    /// Retorna <c>false</c> se o usuário não possui o item.</summary>
    Task<bool> SetEquippedAsync(Guid userId, int cosmeticId, bool equipped, CancellationToken ct = default);
}

public sealed record ShopBalance(int Coins, int Stars);

public sealed record ShopItemDto(
    int     Id,
    string  Name,
    string  Description,
    string  Category,        // chapeu | acessorio | pelagem | expressao
    string  Slot,            // hat | accessory | fur | mood
    string  AssetSlug,       // valor consumido pelo NAVI (bone, gravata, cinza, happy...)
    string  Rarity,          // common | rare | epic | legendary | exclusive
    string? Currency,        // "coins" | "stars" | null (não comprável)
    int?    Price,
    bool    Owned,
    bool    Equipped,
    string? LockedReason);

public sealed record ShopCatalogDto(
    IReadOnlyList<ShopItemDto> Items,
    ShopBalance                Balance);

public enum BuyOutcome
{
    Ok               = 0,
    NotFound         = 1,
    AlreadyOwned     = 2,
    Locked           = 3,   // item de evento / sem preço
    InsufficientFunds = 4
}

public sealed record BuyResultDto(
    BuyOutcome   Outcome,
    int          CosmeticId,
    ShopBalance? Balance,   // saldo novo quando Ok
    string?      Currency,  // moeda usada / faltante
    int?         Price);
