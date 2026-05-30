using System.Linq;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

public class RoomProjectorTests
{
    private static RoomProjector BuildProjector(World world)
    {
        var props = new PropertyRegistry();
        props.RegisterEngineProperty("terrain", "t", PropertyValueType.String,
            appliesTo: new[] { EntityTypes.Room });
        var tags = new TagRegistry();
        tags.RegisterEngineTag("forest", "forest", new[] { EntityTypes.Room }, kind: "biome");
        tags.RegisterEngineTag("safe", "safe", new[] { EntityTypes.Room });
        return new RoomProjector(world, props, tags);
    }

    [Fact]
    public void Projects_room_fields_biome_tags_props_and_exits()
    {
        var world = new World();
        var north = new Room("ns:n", "North Room", "n");
        var room = new Room("ns:r1", "Room One", "A clearing.") { Area = "ns-area" };
        room.AddTag("forest");
        room.AddTag("safe");
        room.SetProperty("terrain", "forest");
        room.SetExit(Direction.North, new Exit("ns:n"));
        world.AddRoom(north);
        world.AddRoom(room);

        var data = BuildProjector(world).Project(room);

        Assert.Equal("ns:r1", data.Id);
        Assert.Equal("ns-area", data.Area);
        Assert.Equal("Room One", data.Name);
        Assert.Equal("A clearing.", data.Description);
        Assert.Equal("forest", data.Biome);
        Assert.Equal(new[] { "safe" }, data.Tags.ToArray());
        Assert.Equal("forest", data.Properties["terrain"]);
        Assert.Equal("ns:n", data.Exits["north"]);
        Assert.Single(data.Neighbors);
        Assert.Equal("North Room", data.Neighbors[0].Name);
    }
}
