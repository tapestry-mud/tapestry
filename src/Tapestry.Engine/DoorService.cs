using Tapestry.Shared;

namespace Tapestry.Engine;

public class DoorService
{
    private readonly World _world;
    private readonly EventBus _eventBus;

    public DoorService(World world, EventBus eventBus)
    {
        _world = world;
        _eventBus = eventBus;
    }

    // ── State changes ─────────────────────────────────────────────────

    public bool Open(Entity actor, Room room, Direction direction)
    {
        var exit = room.GetExit(direction);
        if (exit?.Door == null || !exit.Door.IsClosed) { return false; }

        exit.Door.IsClosed = false;
        SyncReverse(room, direction, d => { d.IsClosed = false; });
        PublishDoorEvent("door.opened", actor, room, direction, exit.Door);
        return true;
    }

    public bool Close(Entity actor, Room room, Direction direction)
    {
        var exit = room.GetExit(direction);
        if (exit?.Door == null || exit.Door.IsClosed) { return false; }

        exit.Door.IsClosed = true;
        SyncReverse(room, direction, d => { d.IsClosed = true; });
        PublishDoorEvent("door.closed", actor, room, direction, exit.Door);
        return true;
    }

    public bool Unlock(Entity actor, Room room, Direction direction)
    {
        var exit = room.GetExit(direction);
        if (exit?.Door == null || !exit.Door.IsLocked) { return false; }

        exit.Door.IsLocked = false;
        SyncReverse(room, direction, d => { d.IsLocked = false; });
        PublishDoorEvent("door.unlocked", actor, room, direction, exit.Door, includeKey: true);
        return true;
    }

    public bool Lock(Entity actor, Room room, Direction direction)
    {
        var exit = room.GetExit(direction);
        if (exit?.Door == null || exit.Door.IsLocked || !exit.Door.IsClosed) { return false; }

        exit.Door.IsLocked = true;
        SyncReverse(room, direction, d => { d.IsLocked = true; });
        PublishDoorEvent("door.locked", actor, room, direction, exit.Door, includeKey: true);
        return true;
    }

    // ── Queries ───────────────────────────────────────────────────────

    public bool CanPass(Room room, Direction direction)
    {
        var exit = room.GetExit(direction);
        if (exit == null) { return false; }
        return exit.Door == null || !exit.Door.IsClosed;
    }

    public DoorState? GetDoor(Room room, Direction direction)
    {
        return room.GetExit(direction)?.Door;
    }

    public bool HasKey(Entity actor, string keyId)
    {
        return actor.Contents.Any(item =>
            string.Equals(item.GetProperty<string>(CommonProperties.TemplateId), keyId,
                          StringComparison.OrdinalIgnoreCase));
    }

    // ── Reset ─────────────────────────────────────────────────────────

    public void ResetDoor(Room room, Direction direction)
    {
        var exit = room.GetExit(direction);
        if (exit?.Door == null) { return; }

        exit.Door.IsClosed = exit.Door.DefaultClosed;
        exit.Door.IsLocked = exit.Door.DefaultLocked;
        SyncReverse(room, direction, d =>
        {
            d.IsClosed = exit.Door.DefaultClosed;
            d.IsLocked = exit.Door.DefaultLocked;
        });
    }

    public void ResetArea(string areaPrefix)
    {
        foreach (var room in _world.AllRooms)
        {
            if (!BelongsToArea(room.Id, areaPrefix)) { continue; }
            foreach (var direction in room.AvailableExits())
            {
                ResetDoor(room, direction);
            }
        }
    }

    // ── Target resolution ─────────────────────────────────────────────

    public Direction? ResolveTarget(Room room, string input)
    {
        // 1. Exact direction match
        if (DirectionExtensions.TryParse(input, out var dir))
        {
            if (room.GetExit(dir) != null) { return dir; }
        }

        // 2. Parse optional ordinal prefix (e.g., "2.gate" → ordinal=2, keyword="gate")
        int ordinal = 0;
        string keyword = input;
        var dotIdx = input.IndexOf('.');
        if (dotIdx > 0 && int.TryParse(input[..dotIdx], out var parsed))
        {
            ordinal = parsed;
            keyword = input[(dotIdx + 1)..];
        }

        // 3. Collect exits with doors matching keyword
        var matches = new List<Direction>();
        foreach (var d in room.AvailableExits())
        {
            var exit = room.GetExit(d)!;
            if (exit.Door == null) { continue; }
            if (exit.Door.Keywords.Any(k =>
                    string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase)))
            {
                matches.Add(d);
            }
        }

        if (matches.Count == 0) { return null; }

        // No ordinal: unambiguous only if one match
        if (ordinal == 0)
        {
            return matches.Count == 1 ? matches[0] : null;
        }

        // Ordinal: 1-indexed
        return ordinal <= matches.Count ? matches[ordinal - 1] : null;
    }

    // ── Private ───────────────────────────────────────────────────────

    private void SyncReverse(Room room, Direction direction, Action<DoorState> mutate)
    {
        var exit = room.GetExit(direction);
        if (exit == null) { return; }
        var targetRoom = _world.GetRoom(exit.TargetRoomId);
        if (targetRoom == null) { return; }
        var reverseExit = targetRoom.GetExit(direction.Opposite());
        if (reverseExit?.Door == null) { return; }
        mutate(reverseExit.Door);
    }

    private void PublishDoorEvent(string type, Entity actor, Room room, Direction direction,
                                  DoorState door, bool includeKey = false)
    {
        var data = new Dictionary<string, object?>
        {
            ["roomId"] = room.Id,
            ["direction"] = direction.ToShortString(),
            ["actorId"] = actor.Id.ToString(),
            ["doorName"] = door.Name
        };
        if (includeKey) { data["keyId"] = door.KeyId; }

        _eventBus.Publish(new GameEvent
        {
            Type = type,
            SourceEntityId = actor.Id,
            RoomId = room.Id,
            Data = data
        });
    }

    private static bool BelongsToArea(string roomId, string areaPrefix)
    {
        return roomId.StartsWith(areaPrefix + ":", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roomId, areaPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
