using Tapestry.Engine.Economy;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;

namespace Tapestry.Engine.Tests.Economy;

public class ShopServiceTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private EconomyConfig _economyConfig = null!;
    private CurrencyService _currency = null!;
    private ItemRegistry _itemRegistry = null!;
    private EquipmentManager _equipmentManager = null!;
    private ShopService _shop = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _economyConfig = new EconomyConfig();
        _economyConfig.Configure(1.2, 0.5);
        _currency = new CurrencyService(_world, _eventBus);
        _itemRegistry = new ItemRegistry();
        _equipmentManager = new EquipmentManager(new SlotRegistry(), _eventBus);
        _shop = new ShopService(_world, _eventBus, _currency, _economyConfig, _itemRegistry, _equipmentManager);

        _itemRegistry.Register(new ItemTemplate
        {
            Id = "core:short-sword",
            Name = "a short sword",
            Type = "item",
            Properties = { ["value"] = 50 }
        });
        _itemRegistry.Register(new ItemTemplate
        {
            Id = "core:leather-cap",
            Name = "a leather cap",
            Type = "item",
            Properties = { ["value"] = 5 }
        });
    }

    private Entity CreateShopkeeper(double? markupOverride = null, double? discountOverride = null)
    {
        var npc = new Entity("npc", "the test vendor");
        npc.AddTag(ShopProperties.ShopTag);
        npc.SetProperty(ShopProperties.Sells, new List<string> { "core:short-sword", "core:leather-cap" });
        if (markupOverride.HasValue)
        {
            npc.SetProperty(ShopProperties.BuyMarkup, markupOverride.Value);
        }
        if (discountOverride.HasValue)
        {
            npc.SetProperty(ShopProperties.SellDiscount, discountOverride.Value);
        }
        _world.TrackEntity(npc);
        return npc;
    }

    private Entity CreatePlayer(int startGold = 0)
    {
        var p = new Entity("player", "TestPlayer");
        p.SetProperty(CurrencyProperties.Gold, startGold);
        _world.TrackEntity(p);
        return p;
    }

    [Fact]
    public void IsShop_ReturnsTrueForShopTaggedEntity()
    {
        Setup();
        var npc = CreateShopkeeper();
        Assert.True(_shop.IsShop(npc));
    }

    [Fact]
    public void IsShop_ReturnsFalseForNonShopEntity()
    {
        Setup();
        var npc = new Entity("npc", "a guard");
        _world.TrackEntity(npc);
        Assert.False(_shop.IsShop(npc));
    }

    [Fact]
    public void ComputeBuyPrice_UsesPerShopOverride_WhenPresent()
    {
        Setup();
        var npc = CreateShopkeeper(markupOverride: 1.4);
        var price = _shop.ComputeBuyPrice(npc, 100);
        Assert.Equal(140, price);
    }

    [Fact]
    public void ComputeBuyPrice_FallsBackToServerDefault_WhenNoOverride()
    {
        Setup();
        var npc = CreateShopkeeper();
        var price = _shop.ComputeBuyPrice(npc, 100);
        Assert.Equal(120, price);
    }

    [Fact]
    public void ComputeSellPrice_UsesPerShopOverride_WhenPresent()
    {
        Setup();
        var npc = CreateShopkeeper(discountOverride: 0.4);
        var price = _shop.ComputeSellPrice(npc, 100);
        Assert.Equal(40, price);
    }

    [Fact]
    public void Buy_ReturnsInsufficientGold_WhenPlayerCannotAfford()
    {
        Setup();
        var player = CreatePlayer(startGold: 0);
        var npc = CreateShopkeeper();
        var result = _shop.Buy(player, npc, "short-sword");
        Assert.Equal(ShopReason.InsufficientGold, result.Reason);
    }

    [Fact]
    public void Sell_RejectsNoSellItem()
    {
        Setup();
        var player = CreatePlayer(startGold: 0);
        var npc = CreateShopkeeper();
        var item = new Entity("item", "a cursed ring");
        item.AddTag(CurrencyProperties.NoSellTag);
        item.SetProperty(CurrencyProperties.Value, 50);
        player.AddToContents(item);
        _world.TrackEntity(item);

        var result = _shop.Sell(player, npc, "cursed ring");
        Assert.Equal(ShopReason.ItemIsNoSell, result.Reason);
    }

    [Fact]
    public void Sell_RejectsZeroValueItem()
    {
        Setup();
        var player = CreatePlayer();
        var npc = CreateShopkeeper();
        var item = new Entity("item", "a pebble");
        item.SetProperty(CurrencyProperties.Value, 0);
        player.AddToContents(item);
        _world.TrackEntity(item);

        var result = _shop.Sell(player, npc, "pebble");
        Assert.Equal(ShopReason.ItemValueZero, result.Reason);
    }
}
