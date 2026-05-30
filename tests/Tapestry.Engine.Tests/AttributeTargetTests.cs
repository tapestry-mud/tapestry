using Tapestry.Engine;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AttributeTargetTests
{
    [Fact]
    public void Entity_is_an_attribute_target()
    {
        var entity = new Entity(EntityTypes.Npc, "goblin");
        IAttributeTarget target = entity; // compiles only if Entity : IAttributeTarget

        target.SetProperty("xp_value", 42);
        target.AddTag("shop");

        Assert.Equal(42, entity.GetRawProperty("xp_value"));
        Assert.True(target.HasTag("shop"));
        Assert.Equal(EntityTypes.Npc, target.Type);
    }

    [Fact]
    public void Room_is_an_attribute_target_and_supports_remove_tag()
    {
        var room = new Room("ns:r1", "Room One", "A room.");
        IAttributeTarget target = room; // compiles only if Room : IAttributeTarget

        target.SetProperty("terrain", "forest");
        target.AddTag("safe");
        Assert.True(target.HasTag("safe"));
        Assert.Equal("forest", room.GetRawProperty("terrain"));
        Assert.Equal(EntityTypes.Room, target.Type);

        target.RemoveTag("safe");
        Assert.False(target.HasTag("safe"));
    }
}
