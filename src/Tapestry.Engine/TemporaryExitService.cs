using Tapestry.Shared;

namespace Tapestry.Engine;

public class TemporaryExitService
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly AreaTickService _areaTick;
    private readonly Dictionary<string, TemporaryExitRecord> _exits = new();
    private readonly object _syncLock = new();

    public TemporaryExitService(World world, EventBus eventBus, AreaTickService areaTick)
    {
        _world = world;
        _eventBus = eventBus;
        _areaTick = areaTick;
        _eventBus.Subscribe("area.tick", OnAreaTickEvent);
    }

    public string CreateExit(string sourceRoomId, string keyword, string targetRoomId,
                             int tickDuration, string displayName = "portal")
    {
        var room = _world.GetRoom(sourceRoomId);
        if (room == null || room.HasKeywordExit(keyword)) { return ""; }

        var areaPrefix = sourceRoomId.Split(':')[0];
        var currentTick = _areaTick.GetAreaState(areaPrefix)?.TickCount ?? 0;

        var record = new TemporaryExitRecord
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = sourceRoomId,
            Keyword = keyword,
            TargetRoomId = targetRoomId,
            ExpiryTickCount = currentTick + tickDuration,
            DisplayName = displayName
        };

        room.SetKeywordExit(keyword, new Exit(targetRoomId) { DisplayName = displayName });
        lock (_syncLock) { _exits[record.Id] = record; }

        _eventBus.Publish(new GameEvent
        {
            Type = "portal.opened",
            RoomId = sourceRoomId,
            Data = new Dictionary<string, object?>
            {
                ["sourceRoomId"] = sourceRoomId,
                ["targetRoomId"] = targetRoomId,
                ["keyword"] = keyword,
                ["displayName"] = displayName,
                ["tickDuration"] = tickDuration
            }
        });

        return record.Id;
    }

    public string CreatePairedExit(string sourceRoomId, string sourceKeyword,
                                    string targetRoomId, string targetKeyword,
                                    int tickDuration, string displayName = "gate")
    {
        var sourceRoom = _world.GetRoom(sourceRoomId);
        var targetRoom = _world.GetRoom(targetRoomId);
        if (sourceRoom == null || targetRoom == null) { return ""; }
        if (sourceRoom.HasKeywordExit(sourceKeyword) || targetRoom.HasKeywordExit(targetKeyword)) { return ""; }

        var areaPrefix = sourceRoomId.Split(':')[0];
        var currentTick = _areaTick.GetAreaState(areaPrefix)?.TickCount ?? 0;
        var expiry = currentTick + tickDuration;

        var sourceId = Guid.NewGuid().ToString();
        var targetId = Guid.NewGuid().ToString();

        var sourceRecord = new TemporaryExitRecord
        {
            Id = sourceId,
            RoomId = sourceRoomId,
            Keyword = sourceKeyword,
            TargetRoomId = targetRoomId,
            ExpiryTickCount = expiry,
            PairedId = targetId,
            DisplayName = displayName
        };
        var targetRecord = new TemporaryExitRecord
        {
            Id = targetId,
            RoomId = targetRoomId,
            Keyword = targetKeyword,
            TargetRoomId = sourceRoomId,
            ExpiryTickCount = expiry,
            PairedId = sourceId,
            DisplayName = displayName
        };

        sourceRoom.SetKeywordExit(sourceKeyword, new Exit(targetRoomId) { DisplayName = displayName });
        targetRoom.SetKeywordExit(targetKeyword, new Exit(sourceRoomId) { DisplayName = displayName });

        lock (_syncLock)
        {
            _exits[sourceId] = sourceRecord;
            _exits[targetId] = targetRecord;
        }

        _eventBus.Publish(new GameEvent
        {
            Type = "portal.opened",
            RoomId = sourceRoomId,
            Data = new Dictionary<string, object?>
            {
                ["sourceRoomId"] = sourceRoomId,
                ["targetRoomId"] = targetRoomId,
                ["keyword"] = sourceKeyword,
                ["displayName"] = displayName,
                ["tickDuration"] = tickDuration
            }
        });

        return sourceId;
    }

    public void RemoveExit(string exitId)
    {
        TemporaryExitRecord? record = null;
        TemporaryExitRecord? paired = null;

        lock (_syncLock)
        {
            if (!_exits.TryGetValue(exitId, out record)) { return; }
            _exits.Remove(exitId);

            if (record.PairedId != null && _exits.TryGetValue(record.PairedId, out paired))
            {
                _exits.Remove(record.PairedId);
            }
        }

        RemoveFromRoom(record);
        PublishClosed(record);

        if (paired != null)
        {
            RemoveFromRoom(paired);
            PublishClosed(paired);
        }
    }

    public void OnAreaTick(string areaPrefix, int tickCount)
    {
        List<TemporaryExitRecord> expired;
        List<TemporaryExitRecord> pairedExpired = new();

        lock (_syncLock)
        {
            expired = _exits.Values
                .Where(r =>
                    r.RoomId.Split(':')[0].Equals(areaPrefix, StringComparison.OrdinalIgnoreCase)
                    && r.ExpiryTickCount <= tickCount)
                .ToList();

            foreach (var r in expired)
            {
                _exits.Remove(r.Id);
                if (r.PairedId != null && _exits.TryGetValue(r.PairedId, out var p))
                {
                    _exits.Remove(r.PairedId);
                    pairedExpired.Add(p);
                }
            }
        }

        foreach (var r in expired)
        {
            RemoveFromRoom(r);
            PublishClosed(r);
        }
        foreach (var p in pairedExpired)
        {
            RemoveFromRoom(p);
        }
    }

    private void OnAreaTickEvent(GameEvent evt)
    {
        var key = evt.Data.ContainsKey("areaId") ? "areaId" : "areaPrefix";
        if (!evt.Data.TryGetValue(key, out var p) || p is not string prefix) { return; }
        if (!evt.Data.TryGetValue("tickCount", out var c) || c == null) { return; }
        OnAreaTick(prefix, Convert.ToInt32(c));
    }

    private void RemoveFromRoom(TemporaryExitRecord record)
    {
        _world.GetRoom(record.RoomId)?.RemoveKeywordExit(record.Keyword);
    }

    private void PublishClosed(TemporaryExitRecord record)
    {
        _eventBus.Publish(new GameEvent
        {
            Type = "portal.closed",
            RoomId = record.RoomId,
            Data = new Dictionary<string, object?>
            {
                ["sourceRoomId"] = record.RoomId,
                ["targetRoomId"] = record.TargetRoomId,
                ["keyword"] = record.Keyword,
                ["displayName"] = record.DisplayName
            }
        });
    }
}
