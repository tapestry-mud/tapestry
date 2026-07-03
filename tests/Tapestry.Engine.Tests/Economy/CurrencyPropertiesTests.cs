using Tapestry.Engine.Economy;
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Tests.Economy;

public class CurrencyPropertiesTests
{
    [Fact]
    public void Value_IsEngineRegistered()
    {
        var registry = new PropertyRegistry();
        CurrencyProperties.Register(registry);
        Assert.True(registry.IsKnown("value"));
    }
}
