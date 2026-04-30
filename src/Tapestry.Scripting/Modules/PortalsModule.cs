using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class PortalsModule : IJintApiModule
{
    private readonly World _world;
    private readonly TemporaryExitService _portals;

    public string Namespace => "portals";

    public PortalsModule(World world, TemporaryExitService portals)
    {
        _world = world;
        _portals = portals;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            create = new Func<string, string, string, int, string, string>(
                (sourceRoomId, keyword, targetRoomId, tickDuration, displayName) =>
                {
                    return _portals.CreateExit(sourceRoomId, keyword, targetRoomId,
                                               tickDuration, displayName);
                }),

            createPaired = new Func<string, string, string, string, int, string, string>(
                (sourceRoomId, sourceKeyword, targetRoomId, targetKeyword, tickDuration, displayName) =>
                {
                    return _portals.CreatePairedExit(sourceRoomId, sourceKeyword,
                                                      targetRoomId, targetKeyword,
                                                      tickDuration, displayName);
                }),

            remove = new Action<string>((exitId) =>
            {
                _portals.RemoveExit(exitId);
            }),

            getKeywordExits = new Func<string, object[]>((roomId) =>
            {
                var room = _world.GetRoom(roomId);
                if (room == null) { return Array.Empty<object>(); }

                return room.KeywordExits
                    .Select(kv =>
                    {
                        var exit = kv.Value;
                        object? door = null;
                        if (exit.Door != null)
                        {
                            door = new
                            {
                                name = exit.Door.Name,
                                isClosed = exit.Door.IsClosed,
                                isLocked = exit.Door.IsLocked
                            };
                        }
                        return (object)new
                        {
                            keyword = kv.Key,
                            targetRoomId = exit.TargetRoomId,
                            name = exit.DisplayName ?? kv.Key,
                            door
                        };
                    })
                    .ToArray();
            })
        };
    }
}
