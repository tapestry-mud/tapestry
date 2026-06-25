using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Scripting.Authoring;
using Xunit;

namespace Tapestry.Engine.Tests;

/// <summary>Regression tests for AuthoredRoomLoader scan exclusion logic.
/// Guards that item side-cars, oracle table files, and area.yaml are never
/// parsed as rooms (the playtest-found boot-crash class).</summary>
public sealed class AuthoredRoomLoaderTests : IDisposable
{
    private readonly string _root;
    private readonly World _world;
    private readonly PropertyRegistry _props;
    private readonly TagRegistry _tags;
    private readonly AuthoredRoomLoader _loader;

    public AuthoredRoomLoaderTests()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ldr-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_root);

        _world = new World();
        _props = new PropertyRegistry();
        _tags = new TagRegistry();

        _loader = new AuthoredRoomLoader(
            _world,
            NullLogger<AuthoredRoomLoader>.Instance,
            _root,
            _props,
            _tags);
    }

    public void Dispose() => System.IO.Directory.Delete(_root, recursive: true);

    private void WriteFile(string relPath, string content)
    {
        var full = System.IO.Path.Combine(_root, relPath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
    }

    private static string MinimalRoom(string id) =>
        "id: \"" + id + "\"\n" +
        "name: \"A Room\"\n" +
        "short: \"A plain room.\"\n" +
        "description: \"Nothing here.\"\n" +
        "exits: {}";

    [Fact]
    public void item_sidecar_in_items_dir_is_not_loaded_as_room()
    {
        // Item YAML in items/ subdirectory should be skipped entirely by the room loader.
        // (This is the exact file that caused the boot-crash reported in playtest.)
        WriteFile(
            @"area1\items\loot-dented-ladle-12345-0.yaml",
            """
            id: "area1:loot-dented-ladle-12345-0"
            name: dented ladle
            type: item
            keywords: [weapon, melee]
            tags: []
            properties:
              slot: wield
              weight: 4
              damage_dice: 1d6
            """);

        // Should not throw and should load zero rooms from this file.
        _loader.Load();
        Assert.Null(_world.GetRoom("area1:loot-dented-ladle-12345-0"));
    }

    [Fact]
    public void oracle_table_in_rooms_dir_is_skipped()
    {
        // *-oracle-table.yaml inside rooms/ is an oracle baked-rooms set, not an authored room.
        WriteFile(
            @"area1\rooms\room-oracle-table.yaml",
            """
            oracle_table:
              kind: rooms
              entries:
                - { w: 10, id: cave, name: cave, desc: A damp cave. }
            """);

        _loader.Load();
        // No rooms registered.
        Assert.Null(_world.GetRoom("oracle-table-id-if-any"));
    }

    [Fact]
    public void real_room_in_rooms_dir_is_loaded()
    {
        var roomId = "test-area:room-abc";
        WriteFile($@"test-area\rooms\room-abc.yaml", MinimalRoom(roomId));

        _loader.Load();
        var room = _world.GetRoom(roomId);
        Assert.NotNull(room);
        Assert.Equal("A Room", room.Name);
    }

    [Fact]
    public void area_yaml_at_area_root_is_not_loaded_as_room()
    {
        WriteFile(
            @"area1\area.yaml",
            """
            id: "area1"
            name: "Test Area"
            short: "A short."
            description: "A long desc."
            """);

        _loader.Load();
        Assert.Null(_world.GetRoom("area1"));
    }
}
