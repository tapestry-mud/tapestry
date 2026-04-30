using Tapestry.Engine;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class DoorsModule : IJintApiModule
{
    private readonly World _world;
    private readonly DoorService _doors;
    private readonly EventBus _eventBus;

    public string Namespace => "doors";

    public DoorsModule(World world, DoorService doors, EventBus eventBus)
    {
        _world = world;
        _doors = doors;
        _eventBus = eventBus;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            open = new Func<string, string, string, bool>((entityIdStr, roomId, dirStr) =>
            {
                var (room, actor, dir) = Resolve(entityIdStr, roomId, dirStr);
                if (room == null || actor == null || dir == null) { return false; }
                return _doors.Open(actor, room, dir.Value);
            }),

            close = new Func<string, string, string, bool>((entityIdStr, roomId, dirStr) =>
            {
                var (room, actor, dir) = Resolve(entityIdStr, roomId, dirStr);
                if (room == null || actor == null || dir == null) { return false; }
                return _doors.Close(actor, room, dir.Value);
            }),

            // "lock" is a C# keyword — exposed to JS as "lockDoor"
            lockDoor = new Func<string, string, string, bool>((entityIdStr, roomId, dirStr) =>
            {
                var (room, actor, dir) = Resolve(entityIdStr, roomId, dirStr);
                if (room == null || actor == null || dir == null) { return false; }
                return _doors.Lock(actor, room, dir.Value);
            }),

            unlock = new Func<string, string, string, bool>((entityIdStr, roomId, dirStr) =>
            {
                var (room, actor, dir) = Resolve(entityIdStr, roomId, dirStr);
                if (room == null || actor == null || dir == null) { return false; }
                return _doors.Unlock(actor, room, dir.Value);
            }),

            canPass = new Func<string, string, bool>((roomId, dirStr) =>
            {
                var room = _world.GetRoom(roomId);
                if (room == null || !DirectionExtensions.TryParse(dirStr, out var dir)) { return false; }
                return _doors.CanPass(room, dir);
            }),

            getDoor = new Func<string, string, object?>((roomId, dirStr) =>
            {
                var room = _world.GetRoom(roomId);
                if (room == null || !DirectionExtensions.TryParse(dirStr, out var dir)) { return null; }
                var door = _doors.GetDoor(room, dir);
                if (door == null) { return null; }
                return new
                {
                    name = door.Name,
                    isClosed = door.IsClosed,
                    isLocked = door.IsLocked,
                    keyId = door.KeyId,
                    isPickable = door.IsPickable,
                    pickDifficulty = door.PickDifficulty
                };
            }),

            hasKey = new Func<string, string, bool>((entityIdStr, keyId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return false; }
                var entity = _world.GetEntity(id);
                if (entity == null) { return false; }
                return _doors.HasKey(entity, keyId);
            }),

            resolveTarget = new Func<string, string, string?>((roomId, input) =>
            {
                var room = _world.GetRoom(roomId);
                if (room == null) { return null; }
                var dir = _doors.ResolveTarget(room, input);
                return dir?.ToShortString();
            }),

            reset = new Action<string, string>((roomId, dirStr) =>
            {
                var room = _world.GetRoom(roomId);
                if (room == null || !DirectionExtensions.TryParse(dirStr, out var dir)) { return; }
                _doors.ResetDoor(room, dir);
            }),

            resetArea = new Action<string>((areaPrefix) =>
            {
                _doors.ResetArea(areaPrefix);
            })
        };
    }

    private (Room? room, Entity? actor, Direction? dir) Resolve(string entityIdStr, string roomId, string dirStr)
    {
        var room = _world.GetRoom(roomId);
        if (room == null) { return (null, null, null); }
        if (!DirectionExtensions.TryParse(dirStr, out var dir)) { return (null, null, null); }
        Entity? actor = null;
        if (Guid.TryParse(entityIdStr, out var id)) { actor = _world.GetEntity(id); }
        if (actor == null) { return (null, null, null); }
        return (room, actor, dir);
    }
}
