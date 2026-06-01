using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class RoomExitRetargetTests
{
    [Fact]
    public void HasExitTo_FindsDirectionalExit()
    {
        var room = new Room("ns:a", "A", "desc");
        room.SetExit(Direction.North, new Exit("ns:target"));

        room.HasExitTo("ns:target").Should().BeTrue();
        room.HasExitTo("ns:other").Should().BeFalse();
    }

    [Fact]
    public void HasExitTo_FindsKeywordExit()
    {
        var room = new Room("ns:a", "A", "desc");
        room.SetKeywordExit("gate", new Exit("ns:target"));

        room.HasExitTo("ns:target").Should().BeTrue();
    }

    [Fact]
    public void RetargetExits_RepointsDirectionalExits()
    {
        var room = new Room("ns:a", "A", "desc");
        room.SetExit(Direction.North, new Exit("ns:old"));
        room.SetExit(Direction.South, new Exit("ns:unrelated"));

        var changed = room.RetargetExits("ns:old", "ns:new");

        changed.Should().BeTrue();
        room.GetExit(Direction.North)!.TargetRoomId.Should().Be("ns:new");
        room.GetExit(Direction.South)!.TargetRoomId.Should().Be("ns:unrelated");
    }

    [Fact]
    public void RetargetExits_RepointsKeywordExits()
    {
        var room = new Room("ns:a", "A", "desc");
        room.SetKeywordExit("gate", new Exit("ns:old"));

        var changed = room.RetargetExits("ns:old", "ns:new");

        changed.Should().BeTrue();
        room.GetKeywordExit("gate")!.TargetRoomId.Should().Be("ns:new");
    }

    [Fact]
    public void RetargetExits_ReturnsFalse_WhenNothingTargetsOldId()
    {
        var room = new Room("ns:a", "A", "desc");
        room.SetExit(Direction.North, new Exit("ns:unrelated"));

        room.RetargetExits("ns:old", "ns:new").Should().BeFalse();
    }
}
