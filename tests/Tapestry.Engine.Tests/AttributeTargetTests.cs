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
}
