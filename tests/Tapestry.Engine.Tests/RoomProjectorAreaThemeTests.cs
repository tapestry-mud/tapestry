using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Shared;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Engine.Tests;

public class RoomProjectorAreaThemeTests
{
    private static RoomProjector NewProjector(World world, AreaRegistry areas)
    {
        var props = new PropertyRegistry();
        props.RegisterEngineProperty("terrain", "t", PropertyValueType.String,
            appliesTo: new[] { EntityTypes.Room });
        var tags = new TagRegistry();
        tags.RegisterEngineTag("forest", "forest", new[] { EntityTypes.Room }, kind: "biome");
        return new RoomProjector(world, props, tags, areas);
    }

    [Fact]
    public void Project_PopulatesAreaThemeAndName_FromRegistry()
    {
        var areas = new AreaRegistry();
        areas.Register(new AreaDefinition
        {
            Id = "road-to-tar-valon",
            Name = "Road to Tar Valon",
            Theme = "Grim pilgrimage.",
        });
        var world = new World();
        var projector = NewProjector(world, areas);

        var room = new Room("ns:r1", "The Road", "A bleak road stretches ahead.") { Area = "road-to-tar-valon" };
        world.AddRoom(room);
        var data = projector.Project(room);

        Assert.Equal("Grim pilgrimage.", data.AreaTheme);
        Assert.Equal("Road to Tar Valon", data.AreaName);
    }

    [Fact]
    public void Project_NoAreaDef_LeavesAreaThemeNull()
    {
        var areas = new AreaRegistry(); // empty
        var world = new World();
        var projector = NewProjector(world, areas);

        var room = new Room("ns:r2", "Lost Room", "No area here.") { Area = "unknown-area" };
        world.AddRoom(room);
        var data = projector.Project(room);

        Assert.Null(data.AreaTheme);
        Assert.Null(data.AreaName);
    }

    [Fact]
    public void AreaTheme_IsNotSerialized()
    {
        // [YamlIgnore] must keep derived area fields out of room side-cars.
        var data = new RoomData
        {
            Id = "r",
            Area = "road-to-tar-valon",
            AreaTheme = "Grim.",
            AreaName = "Road",
        };
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
            .Build();
        var yaml = serializer.Serialize(data);
        Assert.DoesNotContain("area_theme", yaml);
        Assert.DoesNotContain("area_name", yaml);
    }
}
