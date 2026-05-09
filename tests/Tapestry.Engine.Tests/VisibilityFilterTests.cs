using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class VisibilityFilterTests
{
    [Fact]
    public void CanSee_PassThrough_AlwaysTrue()
    {
        var filter = new VisibilityFilter();
        var observer = new Entity("player", "Alice");
        var candidate = new Entity("npc", "Bob");

        var result = filter.CanSee(observer, candidate);

        result.Should().BeTrue();
    }

    [Fact]
    public void GetVisibleEntities_ReturnsAllRoomEntities()
    {
        var filter = new VisibilityFilter();
        var room = new Room("room:1", "The Void", "Nothing here.");
        var alice = new Entity("player", "Alice");
        var bob = new Entity("npc", "Bob");
        room.AddEntity(alice);
        room.AddEntity(bob);
        var observer = new Entity("player", "Observer");

        var visible = filter.GetVisibleEntities(room, observer);

        visible.Should().BeEquivalentTo(new[] { alice, bob });
    }
}
