using System.Linq;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Xunit;

namespace Tapestry.Engine.Tests;

public class RoomPropertiesTests
{
    [Theory]
    [InlineData("terrain")]
    [InlineData("entry_point_description")]
    [InlineData("entry_point_direction")]
    public void RoomProperties_declares_room_property_applies_to_room(string name)
    {
        var registry = new PropertyRegistry();
        RoomProperties.Register(registry);

        var entry = registry.GetAll().SingleOrDefault(p => p.Name == name);

        Assert.NotNull(entry);
        Assert.True(entry!.AppliesToType(EntityTypes.Room));
        Assert.False(entry.AppliesToType(EntityTypes.Player));
    }

    // Regression for the boot crash: ConfigurationModule runs CommonProperties.Register AND
    // RoomProperties.Register against the same registry. Declaring the room properties in BOTH
    // throws "Engine property 'terrain' is already registered" at strict boot. This test
    // reproduces that boot path so the duplicate can't come back (unit tests that exercised
    // each registrar in isolation missed it).
    [Fact]
    public void Common_and_room_property_registration_do_not_collide()
    {
        var registry = new PropertyRegistry();

        var ex = Record.Exception(() =>
        {
            CommonProperties.Register(registry);
            RoomProperties.Register(registry);
        });

        Assert.Null(ex);
    }
}
