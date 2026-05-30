using System.Linq;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Xunit;

namespace Tapestry.Engine.Tests;

public class CommonPropertiesRoomTests
{
    [Theory]
    [InlineData("terrain")]
    [InlineData("entry_point_description")]
    [InlineData("entry_point_direction")]
    public void Declares_room_property_applies_to_room(string name)
    {
        var registry = new PropertyRegistry();
        CommonProperties.Register(registry);

        var entry = registry.GetAll().SingleOrDefault(p => p.Name == name);

        Assert.NotNull(entry);
        Assert.True(entry!.AppliesToType(EntityTypes.Room));
        Assert.False(entry.AppliesToType(EntityTypes.Player));
    }
}
