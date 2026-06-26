using Tapestry.Shared;

namespace Tapestry.Engine.Consequence;

/// <summary>In-memory overlay of what happened to minted rooms. The kind and lifespan are
/// opaque content-defined strings; the engine never reasons about their meaning. A collapsed
/// delta-map keyed (roomId, kind), idempotent on
/// re-stamp. Memory-only by design: it resets on reboot (the mint reloads from its
/// sidecar; consequences evaporate), so connection/runtime deltas can never leak into a
/// harvest. Eviction is routed by lifespan: ephemeral entries clear on the area repop
/// tick; persistent and succession-seed survive until reboot (succession transform is
/// deferred - the tag is recorded but not yet acted on).</summary>
public sealed class ConsequenceOverlay
{
    private readonly World _world;
    private readonly Dictionary<string, Dictionary<string, ConsequenceEntry>> _byRoom = new();
    private readonly object _sync = new();

    public ConsequenceOverlay(World world, EventBus eventBus)
    {
        _world = world;
        eventBus.Subscribe("area.tick", OnAreaTick);
    }

    public void Stamp(string roomId, string kind, string lifespan)
    {
        lock (_sync)
        {
            if (!_byRoom.TryGetValue(roomId, out var kinds))
            {
                kinds = new Dictionary<string, ConsequenceEntry>(StringComparer.OrdinalIgnoreCase);
                _byRoom[roomId] = kinds;
            }
            kinds[kind] = new ConsequenceEntry(kind, lifespan);
        }
    }

    public IReadOnlyList<ConsequenceEntry> List(string roomId)
    {
        lock (_sync)
        {
            if (!_byRoom.TryGetValue(roomId, out var kinds))
            {
                return Array.Empty<ConsequenceEntry>();
            }
            return kinds.Values.ToList();
        }
    }

    public bool Has(string roomId, string kind)
    {
        lock (_sync)
        {
            return _byRoom.TryGetValue(roomId, out var kinds) && kinds.ContainsKey(kind);
        }
    }

    public bool ClearRoom(string roomId)
    {
        lock (_sync)
        {
            return _byRoom.Remove(roomId);
        }
    }

    /// <summary>Runtime-only graph mutation: removes a directional exit from the live
    /// World graph WITHOUT writing a sidecar, and records it as a consequence under the
    /// caller-supplied opaque kind and lifespan (the engine never names the content). Never
    /// persisted - on reboot the sidecar reload restores the exit and the overlay is empty,
    /// so the mutation cleanly evaporates. An empty kind removes the exit without recording.</summary>
    public bool CollapseExit(string roomId, string direction, string kind, string lifespan)
    {
        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return false;
        }
        if (!DirectionExtensions.TryParse(direction ?? "", out var dir))
        {
            return false;
        }
        room.RemoveExit(dir);
        if (!string.IsNullOrEmpty(kind))
        {
            Stamp(roomId, kind, string.IsNullOrEmpty(lifespan) ? "ephemeral" : lifespan);
        }
        return true;
    }

    private void OnAreaTick(GameEvent evt)
    {
        if (evt.Data == null || !evt.Data.TryGetValue("areaId", out var areaIdObj) || areaIdObj is not string areaId)
        {
            return;
        }

        lock (_sync)
        {
            foreach (var (roomId, kinds) in _byRoom)
            {
                var room = _world.GetRoom(roomId);
                if (room == null || !string.Equals(room.Area, areaId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var ephemeral = kinds.Values
                    .Where(e => string.Equals(e.Lifespan, "ephemeral", StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Kind)
                    .ToList();
                foreach (var kind in ephemeral)
                {
                    kinds.Remove(kind);
                }
            }
        }
    }
}
