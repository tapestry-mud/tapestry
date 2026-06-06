using System.Linq;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AreaProjectorTests
{
    private static RoomProjector NewRoomProjector(World world)
    {
        var props = new PropertyRegistry();
        props.RegisterEngineProperty("terrain", "t", PropertyValueType.String,
            appliesTo: new[] { EntityTypes.Room });
        var tags = new TagRegistry();
        tags.RegisterEngineTag("forest", "forest", new[] { EntityTypes.Room }, kind: "biome");
        return new RoomProjector(world, props, tags, new AreaRegistry());
    }

    [Fact]
    public void Project_CopiesAreaFields_AndSamplesRooms()
    {
        var world = new World();
        var area = new AreaDefinition
        {
            Id = "road-to-tar-valon",
            Name = "Road",
            Short = "s",
            Description = "d",
            Theme = "Grim pilgrimage.",
            Lore = "l",
            LevelRange = new[] { 5, 12 }
        };
        var r1 = new Room("road-to-tar-valon-1", "Dusty Track", "A long dusty track winds north.")
        {
            Area = "road-to-tar-valon"
        };
        var r2 = new Room("road-to-tar-valon-2", "Stone Bridge", "An old stone bridge.")
        {
            Area = "road-to-tar-valon"
        };
        world.AddRoom(r1);
        world.AddRoom(r2);

        var projector = new AreaProjector(world, NewRoomProjector(world));
        var data = projector.Project(area);

        Assert.Equal("road-to-tar-valon", data.Id);
        Assert.Equal("Grim pilgrimage.", data.Theme);
        Assert.Equal(new[] { 5, 12 }, data.LevelRange);
        Assert.Equal(2, data.Rooms.Count);
        Assert.Contains(data.Rooms, s => s.Name == "Dusty Track");
        Assert.IsAssignableFrom<Tapestry.Engine.Recommend.IRecommendContext>(data);
    }

    [Fact]
    public void Project_CapsRoomSample()
    {
        var world = new World();
        var area = new AreaDefinition { Id = "big", Name = "Big" };
        for (var i = 0; i < 20; i++)
        {
            world.AddRoom(new Room("big-" + i, "Room " + i, "x") { Area = "big" });
        }
        var projector = new AreaProjector(world, NewRoomProjector(world));
        var data = projector.Project(area);
        Assert.True(data.Rooms.Count <= AreaProjector.RoomSampleCap);
    }
}
