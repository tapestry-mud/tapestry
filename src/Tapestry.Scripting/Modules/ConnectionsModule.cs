using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Scripting.Connections;
using Tapestry.Shared;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ConnectionsModule : IJintApiModule
{
    private readonly World _world;
    private readonly ConnectionLoader _loader;
    private readonly string _serverRootPath;

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public ConnectionsModule(World world, ConnectionLoader loader, string serverRootPath)
    {
        _world = world;
        _loader = loader;
        _serverRootPath = serverRootPath;
    }

    public string Namespace => "connections";

    public object Build(JintEngine jint)
    {
        return new
        {
            create = new Func<string, string, JsValue, string, string, JsValue, string>(
                (fromRoomId, fromType, fromOpts, toRoomId, toType, toOpts) =>
                {
                    var fromSide = BuildSide(fromRoomId, fromType, fromOpts);
                    var toSide = BuildSide(toRoomId, toType, toOpts);

                    var id = DeriveId(fromRoomId, toRoomId);

                    var record = new ConnectionRecord
                    {
                        Id = id,
                        From = fromSide,
                        To = toSide,
                        CreatedBy = "script",
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    ApplySideToRoom(fromSide, toRoomId);
                    ApplySideToRoom(toSide, fromRoomId);

                    var connectionsDir = Path.Combine(_serverRootPath, "connections");
                    Directory.CreateDirectory(connectionsDir);

                    var filePath = Path.Combine(connectionsDir, $"{id}.yaml");
                    var yaml = Serializer.Serialize(record);
                    File.WriteAllText(filePath, yaml);

                    _loader.AddLoaded(record);

                    return id;
                }),

            remove = new Action<string>(connectionId =>
            {
                var record = _loader.Loaded.FirstOrDefault(r => r.Id == connectionId);
                if (record == null) { return; }

                RemoveSideFromRoom(record.From);
                RemoveSideFromRoom(record.To);

                var filePath = Path.Combine(_serverRootPath, "connections", $"{connectionId}.yaml");
                if (File.Exists(filePath)) { File.Delete(filePath); }

                _loader.RemoveLoaded(record);
            }),

            getForRoom = new Func<string, object[]>(roomId =>
            {
                return _loader.Loaded
                    .Where(r => r.From.Room == roomId || r.To.Room == roomId)
                    .Select(MapRecord)
                    .ToArray();
            }),

            getAll = new Func<object[]>(() =>
            {
                return _loader.Loaded
                    .Select(MapRecord)
                    .ToArray();
            })
        };
    }

    private static ConnectionSide BuildSide(string roomId, string type, JsValue opts)
    {
        var side = new ConnectionSide
        {
            Room = roomId,
            Type = type
        };

        if (opts is ObjectInstance optsObj)
        {
            var directionVal = optsObj.Get("direction");
            if (directionVal.Type != Types.Undefined && directionVal.Type != Types.Null)
            {
                side.Direction = directionVal.ToString();
            }

            var keywordVal = optsObj.Get("keyword");
            if (keywordVal.Type != Types.Undefined && keywordVal.Type != Types.Null)
            {
                side.Keyword = keywordVal.ToString();
            }

            var displayNameVal = optsObj.Get("displayName");
            if (displayNameVal.Type != Types.Undefined && displayNameVal.Type != Types.Null)
            {
                side.DisplayName = displayNameVal.ToString();
            }
        }

        return side;
    }

    private void ApplySideToRoom(ConnectionSide side, string targetRoomId)
    {
        if (string.Equals(side.Type, "one-way", StringComparison.OrdinalIgnoreCase)) { return; }

        var room = _world.GetRoom(side.Room);
        if (room == null) { return; }

        if (string.Equals(side.Type, "direction", StringComparison.OrdinalIgnoreCase))
        {
            if (!DirectionExtensions.TryParse(side.Direction ?? "", out var dir)) { return; }
            room.SetExit(dir, new Exit(targetRoomId));
        }
        else if (string.Equals(side.Type, "keyword", StringComparison.OrdinalIgnoreCase))
        {
            var keyword = side.Keyword ?? "";
            room.SetKeywordExit(keyword, new Exit(targetRoomId) { DisplayName = side.DisplayName });
        }
    }

    private void RemoveSideFromRoom(ConnectionSide side)
    {
        if (string.Equals(side.Type, "one-way", StringComparison.OrdinalIgnoreCase)) { return; }

        var room = _world.GetRoom(side.Room);
        if (room == null) { return; }

        if (string.Equals(side.Type, "direction", StringComparison.OrdinalIgnoreCase))
        {
            if (!DirectionExtensions.TryParse(side.Direction ?? "", out var dir)) { return; }
            room.RemoveExit(dir);
        }
        else if (string.Equals(side.Type, "keyword", StringComparison.OrdinalIgnoreCase))
        {
            var keyword = side.Keyword ?? "";
            room.RemoveKeywordExit(keyword);
        }
    }

    private static string DeriveId(string fromRoomId, string toRoomId)
    {
        var raw = $"{fromRoomId}--{toRoomId}";
        return raw.Replace(':', '_');
    }

    private static object MapRecord(ConnectionRecord r)
    {
        return new
        {
            id = r.Id,
            from = new
            {
                room = r.From.Room,
                type = r.From.Type,
                direction = r.From.Direction,
                keyword = r.From.Keyword,
                displayName = r.From.DisplayName
            },
            to = new
            {
                room = r.To.Room,
                type = r.To.Type,
                direction = r.To.Direction,
                keyword = r.To.Keyword,
                displayName = r.To.DisplayName
            },
            createdBy = r.CreatedBy,
            createdAt = r.CreatedAt.ToString("O")
        };
    }
}
