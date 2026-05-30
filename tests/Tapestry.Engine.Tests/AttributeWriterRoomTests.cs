using System.Collections.Generic;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AttributeWriterRoomTests
{
    private static AttributeWriter Build(out PropertyRegistry props, out TagRegistry tags)
    {
        props = new PropertyRegistry();
        props.RegisterEngineProperty("terrain", "Terrain type", PropertyValueType.String,
            appliesTo: new[] { EntityTypes.Room });
        tags = new TagRegistry();
        tags.RegisterEngineTag("safe", "Combat prohibited", new[] { EntityTypes.Room });
        return new AttributeWriter(props, tags);
    }

    [Fact]
    public void Writes_declared_room_property()
    {
        var writer = Build(out _, out _);
        var room = new Room("ns:r1", "Room", "desc");
        var result = writer.Write(room, "terrain", new List<string> { "forest" });
        Assert.True(result.Ok);
        Assert.Equal("forest", room.GetRawProperty("terrain"));
    }

    [Fact]
    public void Rejects_undeclared_room_property()
    {
        var writer = Build(out _, out _);
        var room = new Room("ns:r1", "Room", "desc");
        var result = writer.Write(room, "not_a_real_prop", new List<string> { "x" });
        Assert.False(result.Ok);
    }

    [Fact]
    public void Rejects_property_not_applies_to_room()
    {
        var writer = Build(out var props, out _);
        props.RegisterEngineProperty("xp_value", "xp", PropertyValueType.Int,
            appliesTo: new[] { EntityTypes.Npc });
        var room = new Room("ns:r1", "Room", "desc");
        var result = writer.Write(room, "xp_value", new List<string> { "10" });
        Assert.False(result.Ok);
    }

    [Fact]
    public void Writes_room_tag_on_and_off()
    {
        var writer = Build(out _, out _);
        var room = new Room("ns:r1", "Room", "desc");
        Assert.True(writer.Write(room, "safe", new List<string> { "true" }).Ok);
        Assert.True(room.HasTag("safe"));
        Assert.True(writer.Write(room, "safe", new List<string> { "false" }).Ok);
        Assert.False(room.HasTag("safe"));
    }

    [Fact]
    public void Describe_reads_room_property()
    {
        var writer = Build(out _, out _);
        var room = new Room("ns:r1", "Room", "desc");
        writer.Write(room, "terrain", new List<string> { "forest" });
        var result = writer.Describe(room, "terrain");
        Assert.True(result.Ok);
        Assert.Contains("forest", result.Message);
    }
}
