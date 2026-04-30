using Tapestry.Engine.Economy;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;

namespace Tapestry.Engine.Tests.Economy;

public class ShopEventCancellationTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private CurrencyService _currency = null!;
    private ShopService _shop = null!;
    private ItemRegistry _itemRegistry = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        var economyConfig = new EconomyConfig();
        economyConfig.Configure(1.2, 0.5);
        _currency = new CurrencyService(_world, _eventBus);
        _itemRegistry = new ItemRegistry();
        var equipmentManager = new EquipmentManager(new SlotRegistry(), _eventBus);
        _shop = new ShopService(_world, _eventBus, _currency, economyConfig, _itemRegistry, equipmentManager);

        _itemRegistry.Register(new ItemTemplate
        {
            Id = "core:short-sword",
            Name = "a short sword",
            Type = "item",
            Properties = { ["value"] = 50 }
        });
    }

    [Fact]
    public void CancelledBuyEvent_DoesNotDebitGoldOrDeliverItem()
    {
        Setup();
        _eventBus.Subscribe("shop.buy", evt => evt.Cancelled = true);

        var npc = new Entity("npc", "vendor");
        npc.AddTag(ShopProperties.ShopTag);
        npc.SetProperty(ShopProperties.Sells, new List<string> { "core:short-sword" });
        _world.TrackEntity(npc);

        var player = new Entity("player", "Buyer");
        player.SetProperty(CurrencyProperties.Gold, 1000);
        _world.TrackEntity(player);

        var result = _shop.Buy(player, npc, "short-sword");

        Assert.Equal(ShopReason.ItemNotForSale, result.Reason);
        Assert.Equal(1000, player.GetProperty<int>(CurrencyProperties.Gold));
        Assert.Empty(player.Contents);
    }

    [Fact]
    public void CancelledSellEvent_DoesNotCreditGoldOrDestroyItem()
    {
        Setup();
        _eventBus.Subscribe("shop.sell", evt => evt.Cancelled = true);

        var npc = new Entity("npc", "vendor");
        npc.AddTag(ShopProperties.ShopTag);
        _world.TrackEntity(npc);

        var player = new Entity("player", "Seller");
        player.SetProperty(CurrencyProperties.Gold, 0);
        _world.TrackEntity(player);

        var item = new Entity("item", "a dagger");
        item.SetProperty(CurrencyProperties.Value, 10);
        player.AddToContents(item);
        _world.TrackEntity(item);

        var result = _shop.Sell(player, npc, "dagger");

        Assert.Equal(ShopReason.ItemNotForSale, result.Reason);
        Assert.Equal(0, player.GetProperty<int>(CurrencyProperties.Gold));
        Assert.Contains(item, player.Contents);
    }
}
