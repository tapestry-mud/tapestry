using Tapestry.Engine.Economy;
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Tests.Economy;

public class PlayerGoldPersistenceTests
{
    private PropertyRegistry BuildRegistry()
    {
        var registry = new PropertyRegistry();
        CurrencyProperties.Register(registry);
        return registry;
    }

    [Fact]
    public void Gold_IsRegisteredAsInt()
    {
        var registry = BuildRegistry();
        Assert.Equal(PropertyValueType.Int, registry.GetValueType(CurrencyProperties.Gold));
    }

    [Fact]
    public void NewPlayer_DefaultsGoldToZero()
    {
        var player = new Entity("player", "TestPlayer");
        Assert.Equal(0, player.GetProperty<int>(CurrencyProperties.Gold));
    }

    [Fact]
    public void Gold_IsRegisteredType_IsInt()
    {
        var registry = BuildRegistry();
        Assert.Equal(PropertyValueType.Int, registry.GetValueType(CurrencyProperties.Gold));
    }
}
