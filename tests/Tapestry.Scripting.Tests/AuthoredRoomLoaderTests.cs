using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Authoring;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class AuthoredRoomLoaderTests
{
    [Fact]
    public void Loads_authored_room_into_world_with_props_and_exit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "authrooms-" + Path.GetRandomFileName());
        var roomsDir = Path.Combine(dir, "lf-test", "rooms");
        Directory.CreateDirectory(roomsDir);
        File.WriteAllText(Path.Combine(roomsDir, "anchor.yaml"),
            "id: \"legends-forgotten:anchor\"\n" +
            "area: lf-test\n" +
            "name: \"Anchor\"\n" +
            "description: |\n  The anchor room.\n" +
            "properties:\n  terrain: road\n" +
            "exits:\n  north: \"legends-forgotten:missing\"\n");

        var world = new World();
        var props = new PropertyRegistry();
        props.RegisterEngineProperty("terrain", "t", PropertyValueType.String, appliesTo: new[] { EntityTypes.Room });
        var tags = new TagRegistry();

        var loader = new AuthoredRoomLoader(world, NullLogger<AuthoredRoomLoader>.Instance, dir, props, tags);
        loader.Load();

        var room = world.GetRoom("legends-forgotten:anchor");
        Assert.NotNull(room);
        Assert.Equal("Anchor", room!.Name);
        Assert.Equal("road", room.GetRawProperty("terrain"));
        // Unresolved exit target -> warned + kept (resolution is lazy at lookup), no crash:
        Assert.NotNull(room.GetExit(Tapestry.Shared.Direction.North));

        Directory.Delete(dir, recursive: true);
    }
}
