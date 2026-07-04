using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Engine.Watch;
using Tapestry.Scripting.Modules;
using Tapestry.Shared;

namespace Tapestry.Scripting.Services;

public class ApiMessaging
{
    private readonly World _world;
    private readonly SessionManager _sessions;
    private readonly IGmcpModuleAdapter _gmcp;
    private readonly CommandResponseContext _responseContext;
    private readonly VisibilityFilter _visibility;
    private readonly QuestMarkerService _questMarkerService;
    private string _motd = "";
    private string _motdColor = "";

    public ApiMessaging(
        World world,
        SessionManager sessions,
        IGmcpModuleAdapter gmcp,
        CommandResponseContext responseContext,
        VisibilityFilter visibility,
        QuestMarkerService questMarkerService)
    {
        _world = world;
        _sessions = sessions;
        _gmcp = gmcp;
        _responseContext = responseContext;
        _visibility = visibility;
        _questMarkerService = questMarkerService;
    }

    public void SetMotd(string motd) { _motd = motd; }
    public string GetMotd() => _motd;

    public void SetMotdColor(string motd) { _motdColor = motd; }
    public string GetMotdColor() => _motdColor;

    // --- Core send ---

    public void Send(Guid entityId, string text)
    {
        _sessions.SendToPlayer(entityId, text);

        if (_responseContext.IsSuppressed(entityId))
        {
            return;
        }

        if (!_gmcp.SupportsPackage(entityId, "Response"))
        {
            return;
        }

        var clean = TextSanitizer.Strip(text.TrimEnd('\r', '\n', ' '));
        if (string.IsNullOrWhiteSpace(clean))
        {
            return;
        }

        _gmcp.Send(entityId, "Response.Feedback", new
        {
            status = "ok",
            type = "info",
            message = clean,
            category = "general"
        });
    }

    /// <summary>
    /// Slice C: send to the player but suppress the watch tee for THIS write — a private DM the
    /// anonymous audience must not see. The player still receives the text and GMCP normally; only the
    /// spectator broadcast is gated (via <see cref="WatchBroadcastScope"/>). The viewer pack routes
    /// tell/reply through this; a server without the viewer pack never calls it (snoop sees tells).
    /// </summary>
    public void SendPrivate(Guid entityId, string text)
    {
        WatchBroadcastScope.Run(() => Send(entityId, text));
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

        var text = !string.IsNullOrEmpty(_motdColor) ? _motdColor : _motd;
        var normalized = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
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

    /// <summary>Renders the room the entity stands in: name, description body, exits line,
    /// entity lines. <paramref name="brief"/> (brief mode, tapestry#42) suppresses ONLY the
    /// description body - name, exits, and entity lines are byte-identical in both modes.
    /// Movement passes the player's brief preference; explicit `look` always renders full
    /// (default). GMCP room data (Room.Info / Room.Nearby / Response.Look) is built by
    /// separate paths and never brief.</summary>
    public void SendRoomDescription(string entityIdStr, bool brief = false)
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
            $"<highlight>{room.Name}</highlight>"
        };
        if (!brief)
        {
            lines.Add(room.Description.TrimEnd());
        }

        var exits = room.AvailableExits().Select(d => d.ToShortString()).ToList();
        if (exits.Count > 0)
        {
            lines.Add($"<direction>[Exits: {string.Join(" ", exits)}]</direction>");
        }

        var visibleEntities = _visibility.GetVisibleEntities(room, entity).ToList();

        // Show items on the ground
        var items = visibleEntities
            .Where(e => e.Type == EntityTypes.Item && e.Container == null)
            .ToList();
        foreach (var item in items)
        {
            var itemTemplateId = item.GetProperty<string>(CommonProperties.TemplateId);
            var itemMarker = !string.IsNullOrEmpty(itemTemplateId) && _questMarkerService.HasQuestMarker(entityId, itemTemplateId)
                ? "<highlight>[Quest]</highlight> "
                : "";
            lines.Add($"{itemMarker}<item.common>{item.Name} is here.</item.common>");
        }

        // Show NPCs
        var npcs = visibleEntities
            .Where(e => e.Type == EntityTypes.Npc)
            .ToList();
        foreach (var npc in npcs)
        {
            var npcTemplateId = npc.GetProperty<string>(CommonProperties.TemplateId);
            var npcMarker = !string.IsNullOrEmpty(npcTemplateId) && _questMarkerService.HasQuestMarker(entityId, npcTemplateId)
                ? "<highlight>[Quest]</highlight> "
                : "";
            lines.Add($"{npcMarker}<npc>{npc.Name} is here.</npc>");
        }

        // Show corpses (corpse is a tag, not a type -- entities are type "container")
        var corpses = visibleEntities
            .Where(e => e.HasTag("corpse"))
            .ToList();
        foreach (var corpse in corpses)
        {
            lines.Add($"<item.common>{corpse.Name} is here.</item.common>");
        }

        // Show other players
        var others = visibleEntities
            .Where(e => e.Type == EntityTypes.Player && e.Id != entityId)
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
