using Microsoft.EntityFrameworkCore;
using Unravel.Application.Gamification.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Gamification;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Gamification;

/// <summary>
/// PR 63 — cobre a loja cosmética: catálogo (owned/equipped/preço), compra
/// (débito coins/stars, auto-equip, saldo insuficiente, já possuído, item
/// locked) e equipar/desequipar (1 por slot). EF InMemory.
/// </summary>
public class CosmeticShopServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly CosmeticShopService _sut;
    private readonly Guid _userId;

    // Ids fixos pra asserts determinísticos.
    private const int Bone = 1, Cartola = 2, Coroa = 3, Antenas = 4, Gravata = 5;

    public CosmeticShopServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new CosmeticShopService(_db);

        var user = User.Create("Demo", Email.Create("demo@unravel.test"), "hash");
        user.Coins = 500;
        user.Stars = 15;
        _userId = user.Id;
        _db.User.Add(user);

        _db.NaviCosmetic.AddRange(
            new NaviCosmetic { Id = Bone,    Name = "Boné",     Type = CosmeticType.Hat,       Rarity = CosmeticRarity.Common,    CoinPrice = 150, AssetSlug = "bone" },
            new NaviCosmetic { Id = Cartola, Name = "Cartola",  Type = CosmeticType.Hat,       Rarity = CosmeticRarity.Rare,      CoinPrice = 320, AssetSlug = "cartola" },
            new NaviCosmetic { Id = Coroa,   Name = "Coroa",    Type = CosmeticType.Hat,       Rarity = CosmeticRarity.Legendary, StarPrice = 12,  AssetSlug = "coroa" },
            new NaviCosmetic { Id = Antenas, Name = "Antenas",  Type = CosmeticType.Hat,       Rarity = CosmeticRarity.Exclusive, LockedReason = "Evento", AssetSlug = "antenas" },
            new NaviCosmetic { Id = Gravata, Name = "Gravata",  Type = CosmeticType.Accessory, Rarity = CosmeticRarity.Common,    CoinPrice = 120, AssetSlug = "gravata" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Catalog_MapsPriceOwnedEquippedAndLock()
    {
        await _sut.BuyAsync(_userId, Bone); // possui + equipa

        var cat = await _sut.GetCatalogAsync(_userId);

        Assert.Equal(5, cat.Items.Count);
        var bone = cat.Items.Single(i => i.Id == Bone);
        Assert.True(bone.Owned); Assert.True(bone.Equipped);
        Assert.Equal("coins", bone.Currency); Assert.Equal(150, bone.Price);
        Assert.Equal("hat", bone.Slot); Assert.Equal("chapeu", bone.Category);

        var coroa = cat.Items.Single(i => i.Id == Coroa);
        Assert.Equal("stars", coroa.Currency); Assert.Equal(12, coroa.Price);
        Assert.Equal("legendary", coroa.Rarity);

        var antenas = cat.Items.Single(i => i.Id == Antenas);
        Assert.Null(antenas.Currency);            // não comprável
        Assert.Equal("Evento", antenas.LockedReason);
    }

    [Fact]
    public async Task Buy_WithCoins_DebitsAndCreatesEquipped()
    {
        var r = await _sut.BuyAsync(_userId, Bone);

        Assert.Equal(BuyOutcome.Ok, r.Outcome);
        Assert.Equal(350, r.Balance!.Coins);  // 500 - 150
        var uc = await _db.UserCosmetic.SingleAsync(x => x.UserId == _userId && x.CosmeticId == Bone);
        Assert.True(uc.IsEquipped);
        Assert.Equal(350, (await _db.User.FindAsync(_userId))!.Coins);
    }

    [Fact]
    public async Task Buy_WithStars_DebitsStars()
    {
        var r = await _sut.BuyAsync(_userId, Coroa);
        Assert.Equal(BuyOutcome.Ok, r.Outcome);
        Assert.Equal(3, r.Balance!.Stars);     // 15 - 12
        Assert.Equal(500, r.Balance.Coins);    // coins intactos
    }

    [Fact]
    public async Task Buy_InsufficientFunds_NoDebitNoItem()
    {
        var poor = User.Create("Poor", Email.Create("poor@unravel.test"), "h");
        poor.Coins = 10;
        _db.User.Add(poor); await _db.SaveChangesAsync();

        var r = await _sut.BuyAsync(poor.Id, Bone);

        Assert.Equal(BuyOutcome.InsufficientFunds, r.Outcome);
        Assert.Equal("coins", r.Currency);
        Assert.Equal(10, (await _db.User.FindAsync(poor.Id))!.Coins);  // sem débito
        Assert.False(await _db.UserCosmetic.AnyAsync(x => x.UserId == poor.Id));
    }

    [Fact]
    public async Task Buy_AlreadyOwned_Rejects()
    {
        await _sut.BuyAsync(_userId, Bone);
        var r = await _sut.BuyAsync(_userId, Bone);
        Assert.Equal(BuyOutcome.AlreadyOwned, r.Outcome);
        Assert.Equal(350, (await _db.User.FindAsync(_userId))!.Coins); // debitou só 1x
    }

    [Fact]
    public async Task Buy_LockedItem_Rejects()
    {
        var r = await _sut.BuyAsync(_userId, Antenas);
        Assert.Equal(BuyOutcome.Locked, r.Outcome);
        Assert.False(await _db.UserCosmetic.AnyAsync(x => x.CosmeticId == Antenas));
    }

    [Fact]
    public async Task Buy_AutoEquip_DeactivatesPreviousSameSlot()
    {
        await _sut.BuyAsync(_userId, Bone);     // hat equipado
        await _sut.BuyAsync(_userId, Cartola);  // outro hat → deve trocar

        var bone = await _db.UserCosmetic.SingleAsync(x => x.CosmeticId == Bone);
        var cartola = await _db.UserCosmetic.SingleAsync(x => x.CosmeticId == Cartola);
        Assert.False(bone.IsEquipped);
        Assert.True(cartola.IsEquipped);
    }

    [Fact]
    public async Task SetEquipped_TogglesAndIsOnePerSlot()
    {
        await _sut.BuyAsync(_userId, Bone);
        await _sut.BuyAsync(_userId, Cartola); // cartola equipado, bone não

        Assert.True(await _sut.SetEquippedAsync(_userId, Bone, true)); // troca pra bone
        Assert.True((await _db.UserCosmetic.SingleAsync(x => x.CosmeticId == Bone)).IsEquipped);
        Assert.False((await _db.UserCosmetic.SingleAsync(x => x.CosmeticId == Cartola)).IsEquipped);

        Assert.True(await _sut.SetEquippedAsync(_userId, Bone, false)); // desequipa
        Assert.False((await _db.UserCosmetic.SingleAsync(x => x.CosmeticId == Bone)).IsEquipped);
    }

    [Fact]
    public async Task SetEquipped_NotOwned_ReturnsFalse()
        => Assert.False(await _sut.SetEquippedAsync(_userId, Gravata, true));

    public void Dispose() => _db.Dispose();
}
