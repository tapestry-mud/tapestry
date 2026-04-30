using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests.Doors;

public class DoorServiceTests
{
    // ── DoorState keyword derivation ──────────────────────────────────
    [Fact]
    public void DoorState_Keywords_DerivedFromName()
    {
        var door = new DoorState { Name = "heavy iron gate" };
        door.Keywords.Should().BeEquivalentTo(new[] { "heavy", "iron", "gate" });
    }

    [Fact]
    public void DoorState_Keywords_SingleWord()
    {
        var door = new DoorState { Name = "door" };
        door.Keywords.Should().BeEquivalentTo(new[] { "door" });
    }

    // ── DoorService helpers ───────────────────────────────────────────────
    private World _world = null!;
    private EventBus _eventBus = null!;
    private DoorService _doors = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _doors = new DoorService(_world, _eventBus);
    }

    private (Room roomA, Room roomB) MakeConnectedRooms(DoorState? door = null)
    {
        var roomA = new Room("test:a", "Room A", "");
        var roomB = new Room("test:b", "Room B", "");
        var exitAB = new Exit("test:b") { Door = door };
        var exitBA = new Exit("test:a")
        {
            Door = door != null ? new DoorState
            {
                Name = door.Name,
                IsClosed = door.DefaultClosed,
                IsLocked = door.DefaultLocked,
                DefaultClosed = door.DefaultClosed,
                DefaultLocked = door.DefaultLocked,
                KeyId = door.KeyId
            } : null
        };
        roomA.SetExit(Tapestry.Shared.Direction.North, exitAB);
        roomB.SetExit(Tapestry.Shared.Direction.South, exitBA);
        _world.AddRoom(roomA);
        _world.AddRoom(roomB);
        return (roomA, roomB);
    }

    private Entity MakeActor(string id = "actor")
    {
        var actor = new Entity("player", id);
        _world.TrackEntity(actor);
        return actor;
    }

    // ── Open ──────────────────────────────────────────────────────────────
    [Fact]
    public void Open_ClosedDoor_ReturnsTrueAndOpens()
    {
        Setup();
        var door = new DoorState { Name = "gate", DefaultClosed = true, IsClosed = true };
        var (roomA, _) = MakeConnectedRooms(door);
        var actor = MakeActor();

        var result = _doors.Open(actor, roomA, Tapestry.Shared.Direction.North);

        result.Should().BeTrue();
        roomA.GetExit(Tapestry.Shared.Direction.North)!.Door!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void Open_SyncsReverseExit()
    {
        Setup();
        var door = new DoorState { Name = "gate", DefaultClosed = true, IsClosed = true };
        var (roomA, roomB) = MakeConnectedRooms(door);

        _doors.Open(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        roomB.GetExit(Tapestry.Shared.Direction.South)!.Door!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void Open_AlreadyOpen_ReturnsFalse()
    {
        Setup();
        var door = new DoorState { Name = "gate", IsClosed = false };
        var (roomA, _) = MakeConnectedRooms(door);

        var result = _doors.Open(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        result.Should().BeFalse();
    }

    [Fact]
    public void Open_NoDoor_ReturnsFalse()
    {
        Setup();
        var (roomA, _) = MakeConnectedRooms(door: null);

        var result = _doors.Open(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        result.Should().BeFalse();
    }

    // ── Close ─────────────────────────────────────────────────────────────
    [Fact]
    public void Close_OpenDoor_ReturnsTrueAndCloses()
    {
        Setup();
        var door = new DoorState { Name = "door", IsClosed = false };
        var (roomA, _) = MakeConnectedRooms(door);

        var result = _doors.Close(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        result.Should().BeTrue();
        roomA.GetExit(Tapestry.Shared.Direction.North)!.Door!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Close_SyncsReverseExit()
    {
        Setup();
        var door = new DoorState { Name = "door", IsClosed = false };
        var (roomA, roomB) = MakeConnectedRooms(door);

        _doors.Close(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        roomB.GetExit(Tapestry.Shared.Direction.South)!.Door!.IsClosed.Should().BeTrue();
    }

    // ── Unlock ────────────────────────────────────────────────────────────
    [Fact]
    public void Unlock_LockedDoor_ReturnsTrueAndUnlocks()
    {
        Setup();
        var door = new DoorState { Name = "door", IsClosed = true, IsLocked = true };
        var (roomA, _) = MakeConnectedRooms(door);

        var result = _doors.Unlock(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        result.Should().BeTrue();
        roomA.GetExit(Tapestry.Shared.Direction.North)!.Door!.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Unlock_SyncsReverseExit()
    {
        Setup();
        var door = new DoorState { Name = "door", IsClosed = true, IsLocked = true };
        var (roomA, roomB) = MakeConnectedRooms(door);

        _doors.Unlock(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        roomB.GetExit(Tapestry.Shared.Direction.South)!.Door!.IsLocked.Should().BeFalse();
    }

    // ── Lock ──────────────────────────────────────────────────────────────
    [Fact]
    public void Lock_ClosedUnlockedDoor_ReturnsTrueAndLocks()
    {
        Setup();
        var door = new DoorState { Name = "door", IsClosed = true, IsLocked = false };
        var (roomA, _) = MakeConnectedRooms(door);

        var result = _doors.Lock(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        result.Should().BeTrue();
        roomA.GetExit(Tapestry.Shared.Direction.North)!.Door!.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Lock_OpenDoor_ReturnsFalse()
    {
        Setup();
        var door = new DoorState { Name = "door", IsClosed = false, IsLocked = false };
        var (roomA, _) = MakeConnectedRooms(door);

        var result = _doors.Lock(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        result.Should().BeFalse();
    }

    // ── Events ────────────────────────────────────────────────────────────
    [Fact]
    public void Open_PublishesDoorOpenedEvent()
    {
        Setup();
        var door = new DoorState { Name = "gate", IsClosed = true };
        var (roomA, _) = MakeConnectedRooms(door);
        GameEvent? captured = null;
        _eventBus.Subscribe("door.opened", e => { captured = e; });

        _doors.Open(MakeActor(), roomA, Tapestry.Shared.Direction.North);

        captured.Should().NotBeNull();
        captured!.Data["doorName"].Should().Be("gate");
    }

    // ── ResolveTarget ─────────────────────────────────────────────────────
    [Fact]
    public void ResolveTarget_ExactDirection_ReturnsDirection()
    {
        Setup();
        var (roomA, _) = MakeConnectedRooms();
        var result = _doors.ResolveTarget(roomA, "north");
        result.Should().Be(Tapestry.Shared.Direction.North);
    }

    [Fact]
    public void ResolveTarget_DoorKeyword_ReturnsThatDirection()
    {
        Setup();
        var door = new DoorState { Name = "heavy iron gate" };
        var (roomA, _) = MakeConnectedRooms(door);

        _doors.ResolveTarget(roomA, "gate").Should().Be(Tapestry.Shared.Direction.North);
        _doors.ResolveTarget(roomA, "iron").Should().Be(Tapestry.Shared.Direction.North);
        _doors.ResolveTarget(roomA, "heavy").Should().Be(Tapestry.Shared.Direction.North);
    }

    [Fact]
    public void ResolveTarget_AmbiguousKeyword_ReturnsNull()
    {
        Setup();
        var roomA = new Room("test:a", "Room A", "");
        var roomB = new Room("test:b", "Room B", "");
        var roomC = new Room("test:c", "Room C", "");
        roomA.SetExit(Tapestry.Shared.Direction.North,
            new Exit("test:b") { Door = new DoorState { Name = "door" } });
        roomA.SetExit(Tapestry.Shared.Direction.East,
            new Exit("test:c") { Door = new DoorState { Name = "door" } });
        _world.AddRoom(roomA);
        _world.AddRoom(roomB);
        _world.AddRoom(roomC);

        _doors.ResolveTarget(roomA, "door").Should().BeNull();
    }

    [Fact]
    public void ResolveTarget_OrdinalDisambiguates()
    {
        Setup();
        var roomA = new Room("test:a", "Room A", "");
        var roomB = new Room("test:b", "Room B", "");
        var roomC = new Room("test:c", "Room C", "");
        roomA.SetExit(Tapestry.Shared.Direction.North,
            new Exit("test:b") { Door = new DoorState { Name = "door" } });
        roomA.SetExit(Tapestry.Shared.Direction.East,
            new Exit("test:c") { Door = new DoorState { Name = "door" } });
        _world.AddRoom(roomA);
        _world.AddRoom(roomB);
        _world.AddRoom(roomC);

        _doors.ResolveTarget(roomA, "2.door").Should().NotBeNull();
    }

    // ── HasKey ────────────────────────────────────────────────────────────
    [Fact]
    public void HasKey_ActorHasMatchingItem_ReturnsTrue()
    {
        Setup();
        var actor = new Entity("player", "Rand");
        var key = new Entity("item", "an iron key");
        key.SetProperty(CommonProperties.TemplateId, "core:key-iron");
        actor.AddToContents(key);
        _world.TrackEntity(actor);

        _doors.HasKey(actor, "core:key-iron").Should().BeTrue();
    }

    [Fact]
    public void HasKey_NoMatchingItem_ReturnsFalse()
    {
        Setup();
        var actor = new Entity("player", "Rand");
        _world.TrackEntity(actor);

        _doors.HasKey(actor, "core:key-iron").Should().BeFalse();
    }

    // ── Reset ─────────────────────────────────────────────────────────────
    [Fact]
    public void ResetDoor_RestoresToDefaults()
    {
        Setup();
        var door = new DoorState
        {
            Name = "door",
            DefaultClosed = true,
            DefaultLocked = true,
            IsClosed = false,
            IsLocked = false
        };
        var (roomA, _) = MakeConnectedRooms(door);

        _doors.ResetDoor(roomA, Tapestry.Shared.Direction.North);

        roomA.GetExit(Tapestry.Shared.Direction.North)!.Door!.IsClosed.Should().BeTrue();
        roomA.GetExit(Tapestry.Shared.Direction.North)!.Door!.IsLocked.Should().BeTrue();
    }
}
