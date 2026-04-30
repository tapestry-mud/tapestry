using Tapestry.Engine.Economy;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Economy;

public class CurrencyServiceTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private CurrencyService _currency = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _currency = new CurrencyService(_world, _eventBus);
    }

    private Entity CreatePlayer(string name = "TestPlayer")
    {
        var e = new Entity("player", name);
        _world.TrackEntity(e);
        return e;
    }

    private Entity CreateCurrencyItem(int value, string name = "a coin pouch")
    {
        var item = new Entity("item", name);
        item.AddTag(CurrencyProperties.CurrencyTag);
        item.SetProperty(CurrencyProperties.Value, value);
        _world.TrackEntity(item);
        return item;
    }

    [Fact]
    public void AutoConvert_CreditGoldAndDestroysItem_WhenPlayerPicksCurrencyItem()
    {
        Setup();
        var player = CreatePlayer();
        var item = CreateCurrencyItem(25);
        player.AddToContents(item);

        var result = _currency.TryAutoConvert(player, item);

        Assert.True(result);
        Assert.Equal(25, player.GetProperty<int>(CurrencyProperties.Gold));
        Assert.Null(_world.GetEntity(item.Id));
    }

    [Fact]
    public void AutoConvert_SkipsConversion_WhenNoCurrencyTag()
    {
        Setup();
        var player = CreatePlayer();
        var item = new Entity("item", "a dagger");
        item.SetProperty(CurrencyProperties.Value, 10);
        _world.TrackEntity(item);
        player.AddToContents(item);

        var result = _currency.TryAutoConvert(player, item);

        Assert.False(result);
        Assert.Equal(0, player.GetProperty<int>(CurrencyProperties.Gold));
        Assert.NotNull(_world.GetEntity(item.Id));
    }

    [Fact]
    public void AutoConvert_SkipsConversion_WhenValueIsZero()
    {
        Setup();
        var player = CreatePlayer();
        var item = CreateCurrencyItem(0);
        player.AddToContents(item);

        var result = _currency.TryAutoConvert(player, item);

        Assert.False(result);
        Assert.Equal(0, player.GetProperty<int>(CurrencyProperties.Gold));
    }

    [Fact]
    public void AutoConvert_SkipsConversion_WhenDestinationIsNotPlayer()
    {
        Setup();
        var chest = new Entity("item", "a chest");
        _world.TrackEntity(chest);
        var item = CreateCurrencyItem(10);
        chest.AddToContents(item);

        var result = _currency.TryAutoConvert(chest, item);

        Assert.False(result);
        Assert.NotNull(_world.GetEntity(item.Id));
    }

    [Fact]
    public void AddGold_ClampsAtZero_WhenDeltaWouldGoNegative()
    {
        Setup();
        var player = CreatePlayer();
        _currency.AddGold(player, 10, "test");
        _currency.AddGold(player, -100, "test");

        Assert.Equal(0, player.GetProperty<int>(CurrencyProperties.Gold));
    }

    [Fact]
    public void SetGold_RefusesNegativeInput()
    {
        Setup();
        var player = CreatePlayer();

        Assert.Throws<ArgumentOutOfRangeException>(() => _currency.SetGold(player, -1, "test"));
    }

    [Fact]
    public void AddGold_PublishesCreditedEvent()
    {
        Setup();
        var player = CreatePlayer();
        GameEvent? received = null;
        _eventBus.Subscribe("currency.credited", e => received = e);

        _currency.AddGold(player, 50, "test");

        Assert.NotNull(received);
        Assert.Equal(50, (int)received!.Data["amount"]!);
    }
}
