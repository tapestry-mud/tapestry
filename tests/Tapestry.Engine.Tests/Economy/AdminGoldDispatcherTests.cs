using Tapestry.Engine.Economy;

namespace Tapestry.Engine.Tests.Economy;

public class AdminGoldDispatcherTests
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

    private Entity CreatePlayer(int startGold = 0)
    {
        var p = new Entity("player", "Target");
        p.SetProperty(CurrencyProperties.Gold, startGold);
        _world.TrackEntity(p);
        return p;
    }

    [Fact]
    public void GrantGold_Increments()
    {
        Setup();
        var player = CreatePlayer(startGold: 0);
        _currency.AddGold(player, 500, "admin:grant");
        Assert.Equal(500, player.GetProperty<int>(CurrencyProperties.Gold));
    }

    [Fact]
    public void GrantGoldNegative_ClampsAtZero()
    {
        Setup();
        var player = CreatePlayer(startGold: 10);
        _currency.AddGold(player, -100, "admin:grant");
        Assert.Equal(0, player.GetProperty<int>(CurrencyProperties.Gold));
    }

    [Fact]
    public void SetGold_SetsAbsolute()
    {
        Setup();
        var player = CreatePlayer(startGold: 50);
        _currency.SetGold(player, 1000, "admin:set");
        Assert.Equal(1000, player.GetProperty<int>(CurrencyProperties.Gold));
    }

    [Fact]
    public void SetGoldNegative_Refused()
    {
        Setup();
        var player = CreatePlayer(startGold: 50);
        Assert.Throws<ArgumentOutOfRangeException>(() => _currency.SetGold(player, -1, "admin:set"));
    }
}
