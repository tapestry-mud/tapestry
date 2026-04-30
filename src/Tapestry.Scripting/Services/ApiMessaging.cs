using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Scripting.Services;

public class ApiMessaging
{
    private readonly World _world;
    private readonly SessionManager _sessions;
    private string _motd = "";

    public ApiMessaging(World world, SessionManager sessions)
    {
        _world = world;
        _sessions = sessions;
    }

    public void SetMotd(string motd)
    {
        _motd = motd;
    }

    public string GetMotd() => _motd;

    // --- Core send ---

    public void Send(Guid entityId, string text)
    {
        _sessions.SendToPlayer(entityId, text);
    }

    public void SendToRoomExcept(string roomId, string excludeIdStr, string text)
    {
        Guid? excludeId = null;
        if (Guid.TryParse(excludeIdStr, out var parsed))
        {
            excludeId = parsed;
        }

        _sessions.SendToRoom(roomId, text, excludeId);
    }

    public void SendToRoomExceptMany(string roomId, string[] excludeIdStrs, string text)
    {
        var excludeIds = new HashSet<Guid>();
        foreach (var idStr in excludeIdStrs)
        {
            if (Guid.TryParse(idStr, out var parsed))
            {
                excludeIds.Add(parsed);
            }
        }

        _sessions.SendToRoom(roomId, text, excludeIds);
    }

    public void SendToAll(string text, string excludeIdStr)
    {
        Guid? excludeId = null;
        if (Guid.TryParse(excludeIdStr, out var parsed))
        {
            excludeId = parsed;
        }

        _sessions.SendToAll(text, excludeId);
    }

    public void SendToRoom(string roomId, string text)
    {
        _sessions.SendToRoom(roomId, text);
    }

    public void SendMotd(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return;
        }

        var normalized = _motd.Replace("\r\n", "\n").Replace("\n", "\r\n");
        Send(entityId, normalized + "\r\n");
    }

    public void SendToRoomSkipSleeping(string roomId, string excludeIdStr, string text)
    {
        Guid? excludeId = Guid.TryParse(excludeIdStr, out var parsed) ? parsed : null;
        foreach (var session in _sessions.AllSessions)
        {
            if (session.PlayerEntity.LocationRoomId != roomId) { continue; }
            if (session.PlayerEntity.Id == excludeId) { continue; }
            var restState = session.PlayerEntity.GetProperty<string?>("rest_state") ?? "awake";
            if (restState == "sleeping") { continue; }
            session.Send(text);
        }
    }

    public void SendRoomDescription(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null || entity.LocationRoomId == null)
        {
            return;
        }

        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null)
        {
            return;
        }

        var lines = new List<string>
        {
            "",
            $"<highlight>{room.Name}</highlight>",
            room.Description.TrimEnd()
        };

        var exits = room.AvailableExits().Select(d => d.ToShortString()).ToList();
        if (exits.Count > 0)
        {
            lines.Add($"<direction>[Exits: {string.Join(" ", exits)}]</direction>");
        }

        // Show items on the ground
        var items = room.Entities
            .Where(e => e.HasTag("item") && e.Container == null)
            .ToList();
        foreach (var item in items)
        {
            lines.Add($"<item.common>{item.Name} is here.</item.common>");
        }

        // Show NPCs
        var npcs = room.Entities
            .Where(e => e.HasTag("npc"))
            .ToList();
        foreach (var npc in npcs)
        {
            lines.Add($"<npc>{npc.Name} is here.</npc>");
        }

        // Show corpses
        var corpses = room.Entities
            .Where(e => e.HasTag("corpse"))
            .ToList();
        foreach (var corpse in corpses)
        {
            lines.Add($"<item.common>{corpse.Name} is here.</item.common>");
        }

        // Show other players
        var others = room.Entities
            .Where(e => e.HasTag("player") && e.Id != entityId)
            .Select(e => e.Name)
            .ToList();
        foreach (var other in others)
        {
            lines.Add($"<player>{other} is here.</player>");
        }

        lines.Add("");
        Send(entityId, string.Join("\r\n", lines));
    }
}
