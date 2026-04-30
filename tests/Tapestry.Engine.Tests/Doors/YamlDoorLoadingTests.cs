using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Engine.Tests.Doors;

public class YamlDoorLoadingTests
{
    // ── Directional exits with doors ──────────────────────────────────
    [Fact]
    public void LoadRoom_StringExit_StillWorks()
    {
        var yaml = """
            id: "core:town"
            name: "Town"
            description: "A town."
            exits:
              north: "core:inn"
            """;
        var result = YamlContentLoader.LoadRoom(yaml);
        result.Room.GetExit(Tapestry.Shared.Direction.North)!.TargetRoomId.Should().Be("core:inn");
        result.Room.GetExit(Tapestry.Shared.Direction.North)!.Door.Should().BeNull();
    }

    [Fact]
    public void LoadRoom_ObjectExitNoDoor_Works()
    {
        var yaml = """
            id: "core:town"
            name: "Town"
            description: "A town."
            exits:
              north:
                target: "core:inn"
            """;
        var result = YamlContentLoader.LoadRoom(yaml);
        result.Room.GetExit(Tapestry.Shared.Direction.North)!.TargetRoomId.Should().Be("core:inn");
        result.Room.GetExit(Tapestry.Shared.Direction.North)!.Door.Should().BeNull();
    }

    [Fact]
    public void LoadRoom_ExitWithDoor_ParsesDoorProperties()
    {
        var yaml = """
            id: "core:dungeon"
            name: "Dungeon"
            description: "Dark."
            exits:
              north:
                target: "core:hall"
                door:
                  name: "heavy iron door"
                  closed: true
                  locked: true
                  key: "core:key-iron"
                  pickable: true
                  pick_difficulty: 75
            """;
        var result = YamlContentLoader.LoadRoom(yaml);
        var door = result.Room.GetExit(Tapestry.Shared.Direction.North)!.Door!;
        door.Name.Should().Be("heavy iron door");
        door.IsClosed.Should().BeTrue();
        door.IsLocked.Should().BeTrue();
        door.KeyId.Should().Be("core:key-iron");
        door.IsPickable.Should().BeTrue();
        door.PickDifficulty.Should().Be(75);
        door.DefaultClosed.Should().BeTrue();
        door.DefaultLocked.Should().BeTrue();
    }

    [Fact]
    public void LoadRoom_DoorMirrored_ToReverseExit()
    {
        var dungeonYaml = """
            id: "core:dungeon"
            name: "Dungeon"
            description: "Dark."
            exits:
              north:
                target: "core:hall"
                door:
                  name: "iron door"
                  closed: true
            """;
        var hallYaml = """
            id: "core:hall"
            name: "Hall"
            description: "Bright."
            exits:
              south: "core:dungeon"
            """;
        var dungeonResult = YamlContentLoader.LoadRoom(dungeonYaml);
        var hallResult = YamlContentLoader.LoadRoom(hallYaml);
        var allRooms = new List<Room> { dungeonResult.Room, hallResult.Room };
        YamlContentLoader.MirrorDoorsAcrossRooms(allRooms);

        var hallRoom = allRooms.First(r => r.Id == "core:hall");
        var reverseDoor = hallRoom.GetExit(Tapestry.Shared.Direction.South)!.Door;
        reverseDoor.Should().NotBeNull();
        reverseDoor!.Name.Should().Be("iron door");
        reverseDoor.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void LoadRoom_BothSidesDefineDoor_NotMirrored()
    {
        var dungeonYaml = """
            id: "core:dungeon"
            name: "Dungeon"
            description: "Dark."
            exits:
              north:
                target: "core:hall"
                door:
                  name: "iron door"
                  closed: true
            """;
        var hallYaml = """
            id: "core:hall"
            name: "Hall"
            description: "Bright."
            exits:
              south:
                target: "core:dungeon"
                door:
                  name: "wooden door"
                  closed: false
            """;
        var dungeonResult = YamlContentLoader.LoadRoom(dungeonYaml);
        var hallResult = YamlContentLoader.LoadRoom(hallYaml);
        var allRooms = new List<Room> { dungeonResult.Room, hallResult.Room };
        YamlContentLoader.MirrorDoorsAcrossRooms(allRooms);

        var hallRoom = allRooms.First(r => r.Id == "core:hall");
        hallRoom.GetExit(Tapestry.Shared.Direction.South)!.Door!.Name.Should().Be("wooden door");
    }

    // ── Keyword exits ─────────────────────────────────────────────────
    [Fact]
    public void LoadRoom_KeywordExits_Loaded()
    {
        var yaml = """
            id: "core:tower"
            name: "Tower"
            description: "Tall."
            keyword_exits:
              mirror:
                target: "core:scrying-room"
                name: "a shimmering mirror"
            """;
        var result = YamlContentLoader.LoadRoom(yaml);
        var exit = result.Room.GetKeywordExit("mirror");
        exit.Should().NotBeNull();
        exit!.TargetRoomId.Should().Be("core:scrying-room");
        exit.DisplayName.Should().Be("a shimmering mirror");
    }

    [Fact]
    public void LoadRoom_KeywordExitWithDoor_Loaded()
    {
        var yaml = """
            id: "core:tower"
            name: "Tower"
            description: "Tall."
            keyword_exits:
              mirror:
                target: "core:scrying-room"
                name: "enchanted mirror"
                door:
                  name: "mirror surface"
                  closed: true
            """;
        var result = YamlContentLoader.LoadRoom(yaml);
        var exit = result.Room.GetKeywordExit("mirror");
        exit!.Door.Should().NotBeNull();
        exit.Door!.Name.Should().Be("mirror surface");
        exit.Door.IsClosed.Should().BeTrue();
    }
}
