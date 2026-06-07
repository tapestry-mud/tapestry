using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public void Skips_area_sidecar_does_not_parse_it_as_a_room()
    {
        var dir = Path.Combine(Path.GetTempPath(), "authrooms-" + Path.GetRandomFileName());
        var areaDir = Path.Combine(dir, "lonely-road");
        var roomsDir = Path.Combine(areaDir, "rooms");
        Directory.CreateDirectory(roomsDir);
        // An area side-car sits next to the rooms/ dir; the room loader must NOT parse it.
        File.WriteAllText(Path.Combine(areaDir, "area.yaml"),
            "area:\n  id: lonely-road\n  name: Lonely Road\n  theme: A quiet road.\n");
        // A real authored room (no exits -> no exit warnings to confuse the assertion).
        File.WriteAllText(Path.Combine(roomsDir, "anchor.yaml"),
            "id: \"legends-forgotten:lonely-road-anchor\"\n" +
            "area: lonely-road\n" +
            "name: \"Anchor\"\n" +
            "description: |\n  The anchor.\n");

        var world = new World();
        var logger = new CapturingLogger<AuthoredRoomLoader>();
        var loader = new AuthoredRoomLoader(world, logger, dir, new PropertyRegistry(), new TagRegistry());
        loader.Load();

        // The real room loads...
        Assert.NotNull(world.GetRoom("legends-forgotten:lonely-road-anchor"));
        // ...and the area side-car is skipped, not failed-to-parse as a room (the boot warning).
        Assert.DoesNotContain(logger.Entries, e => e.Contains("area.yaml"));
        Assert.DoesNotContain(logger.Entries, e => e.Contains("Failed to load authored room"));

        Directory.Delete(dir, recursive: true);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public readonly List<string> Entries = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
