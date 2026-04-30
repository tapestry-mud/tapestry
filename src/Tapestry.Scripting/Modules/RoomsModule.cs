using Tapestry.Engine;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class RoomsModule : IJintApiModule
{
    private readonly World _world;

    public RoomsModule(World world)
    {
        _world = world;
    }

    public string Namespace => "rooms";

    public object Build(JintEngine jint)
    {
        return new
        {
            getAll = new Func<object[]>(() =>
            {
                return _world.AllRooms
                    .Select(room => (object)new { id = room.Id, name = room.Name })
                    .ToArray();
            }),

            getByPack = new Func<string, object[]>(packName =>
            {
                var prefix = packName + ":";
                return _world.AllRooms
                    .Where(r => r.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(room => (object)new { id = room.Id, name = room.Name })
                    .ToArray();
            }),

            getEntryPoints = new Func<string, object[]>(packName =>
            {
                var prefix = packName + ":";
                return _world.AllRooms
                    .Where(r => r.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Where(r => r.HasTag("entry-point"))
                    .Select(room => (object)new
                    {
                        id = room.Id,
                        name = room.Name,
                        entry_point_description = room.GetProperty<string>("entry_point_description") ?? "",
                        entry_point_direction = room.GetProperty<string>("entry_point_direction") ?? ""
                    })
                    .ToArray();
            }),

            getExits = new Func<string, object[]>(roomId =>
            {
                var room = _world.GetRoom(roomId);
                if (room == null) { return Array.Empty<object>(); }

                var exitsList = new List<object>();

                // Directional exits
                foreach (var direction in Enum.GetValues<Direction>())
                {
                    var exit = room.GetExit(direction);

                    if (exit != null)
                    {
                        exitsList.Add(new
                        {
                            type = "direction",
                            direction = direction.ToString(),
                            targetRoomId = exit.TargetRoomId,
                            displayName = exit.DisplayName ?? "",
                            occupied = true
                        });
                    }
                    else
                    {
                        exitsList.Add(new
                        {
                            type = "direction",
                            direction = direction.ToString(),
                            occupied = false
                        });
                    }
                }

                // Keyword exits
                foreach (var kvp in room.KeywordExits)
                {
                    exitsList.Add(new
                    {
                        type = "keyword",
                        keyword = kvp.Key,
                        targetRoomId = kvp.Value.TargetRoomId,
                        displayName = kvp.Value.DisplayName ?? "",
                        occupied = true
                    });
                }

                return exitsList.ToArray();
            })
        };
    }
}
