using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Shared;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Scripting.Connections;

public class ConnectionLoader
{
    private readonly World _world;
    private readonly ILogger<ConnectionLoader> _logger;
    private readonly string _serverRootPath;
    private readonly List<ConnectionRecord> _loaded = new();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public IReadOnlyList<ConnectionRecord> Loaded => _loaded;

    public ConnectionLoader(World world, ILogger<ConnectionLoader> logger, string serverRootPath)
    {
        _world = world;
        _logger = logger;
        _serverRootPath = serverRootPath;
    }

    public void Load()
    {
        var connectionsDir = Path.Combine(_serverRootPath, "connections");
        if (!Directory.Exists(connectionsDir))
        {
            _logger.LogInformation("No connections directory found at {Path}, skipping", connectionsDir);
            return;
        }

        var files = Directory.GetFiles(connectionsDir, "*.yaml", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f)
                             .ToList();

        var seen = new HashSet<(string From, string To)>();
        var warnings = 0;

        foreach (var file in files)
        {
            ConnectionRecord record;
            try
            {
                var yaml = File.ReadAllText(file);
                record = Deserializer.Deserialize<ConnectionRecord>(yaml);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to parse connection file {File}: {Error}", file, ex.Message);
                warnings++;
                continue;
            }

            var pairKey = (record.From.Room, record.To.Room);
            var reversePairKey = (record.To.Room, record.From.Room);
            if (seen.Contains(pairKey) || seen.Contains(reversePairKey))
            {
                _logger.LogWarning("Duplicate connection ({From} <-> {To}) in {File}, skipping",
                    record.From.Room, record.To.Room, file);
                warnings++;
                continue;
            }

            seen.Add(pairKey);

            var success = ApplyConnection(record, file, ref warnings);
            if (success)
            {
                _loaded.Add(record);
            }
        }

        _logger.LogInformation("Loaded {Count} connections ({Warnings} warnings)", _loaded.Count, warnings);
    }

    private bool ApplyConnection(ConnectionRecord record, string file, ref int warnings)
    {
        var fromRoom = _world.GetRoom(record.From.Room);
        if (fromRoom == null)
        {
            _logger.LogWarning("Connection {File}: room '{Room}' not found, skipping entire connection",
                file, record.From.Room);
            warnings++;
            return false;
        }

        var toRoom = _world.GetRoom(record.To.Room);
        if (toRoom == null)
        {
            _logger.LogWarning("Connection {File}: room '{Room}' not found, skipping entire connection",
                file, record.To.Room);
            warnings++;
            return false;
        }

        ApplySide(record.From, record.To.Room, fromRoom, file, ref warnings);
        ApplySide(record.To, record.From.Room, toRoom, file, ref warnings);

        return true;
    }

    private void ApplySide(ConnectionSide side, string targetRoomId, Room room,
                           string file, ref int warnings)
    {
        if (string.Equals(side.Type, "one-way", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ApplyDirectionOrKeyword(side, targetRoomId, room, file, ref warnings);
    }

    private void ApplyDirectionOrKeyword(ConnectionSide side, string targetRoomId, Room room,
                                         string file, ref int warnings)
    {
        if (string.Equals(side.Type, "direction", StringComparison.OrdinalIgnoreCase))
        {
            var dirStr = side.Direction ?? "";
            if (!DirectionExtensions.TryParse(dirStr, out var dir))
            {
                _logger.LogWarning("Connection {File}: invalid direction '{Dir}' on room {Room}, skipping side",
                    file, dirStr, room.Id);
                warnings++;
                return;
            }

            if (room.GetExit(dir) != null)
            {
                _logger.LogWarning("Direction {Dir} on {Room} already occupied, skipping side",
                    dir, room.Id);
                warnings++;
                return;
            }

            room.SetExit(dir, new Exit(targetRoomId));
        }
        else if (string.Equals(side.Type, "keyword", StringComparison.OrdinalIgnoreCase))
        {
            var keyword = side.Keyword ?? "";
            if (room.HasKeywordExit(keyword))
            {
                _logger.LogWarning("Keyword {Keyword} on {Room} already occupied, skipping side",
                    keyword, room.Id);
                warnings++;
                return;
            }

            room.SetKeywordExit(keyword, new Exit(targetRoomId) { DisplayName = side.DisplayName });
        }
    }
}
