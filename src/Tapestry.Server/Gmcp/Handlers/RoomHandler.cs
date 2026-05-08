using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;
using Tapestry.Shared;

namespace Tapestry.Server.Gmcp.Handlers;

public class RoomHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;

    public string Name => "Room";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Room.Info", "Room.Nearby", "Room.WrongDir" };

    public RoomHandler(
        IGmcpConnectionManager connectionManager,
        SessionManager sessions,
        World world,
        EventBus eventBus)
    {
        _connectionManager = connectionManager;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
    }

    public void Configure()
    {
        _eventBus.Subscribe("player.moved", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            var entity = _world.GetEntity(evt.SourceEntityId.Value);
            if (entity == null) { return; }
            SendRoomNearby(evt.SourceEntityId.Value, entity);
            SendRoomInfo(evt.SourceEntityId.Value, entity);
        });

        _eventBus.Subscribe("player.move.failed", evt =>
        {
            Guid entityId;
            if (evt.SourceEntityId.HasValue)
            {
                entityId = evt.SourceEntityId.Value;
            }
            else if (evt.Data.TryGetValue("entityId", out var idObj)
                && Guid.TryParse(idObj?.ToString(), out var parsed))
            {
                entityId = parsed;
            }
            else { return; }

            _connectionManager.Send(entityId, "Room.WrongDir", "");
        });
    }

    public void SendBurst(string connectionId, object entity)
    {
        var e = (Entity)entity;
        SendRoomNearbyToConnection(connectionId, e);
        SendRoomInfoToConnection(connectionId, e);
    }

    private void SendRoomInfo(Guid entityId, Entity entity)
    {
        if (entity.LocationRoomId == null) { return; }
        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null) { return; }
        _connectionManager.Send(entityId, "Room.Info", BuildRoomInfoPayload(room, entity));
    }

    private void SendRoomInfoToConnection(string connectionId, Entity entity)
    {
        if (entity.LocationRoomId == null) { return; }
        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null) { return; }
        _connectionManager.Send(connectionId, "Room.Info", BuildRoomInfoPayload(room, entity));
    }

    private static object BuildRoomInfoPayload(Room room, Entity entity)
    {
        var exits = new Dictionary<string, string?>();
        var doors = new Dictionary<string, object>();

        foreach (var dir in room.AvailableExits())
        {
            var exit = room.GetExit(dir);
            if (exit == null) { continue; }
            var shortDir = dir.ToShortString().ToLower();
            exits[shortDir] = exit.TargetRoomId;
            if (exit.Door != null)
            {
                doors[shortDir] = new { isClosed = exit.Door.IsClosed, isLocked = exit.Door.IsLocked };
            }
        }

        return new
        {
            num = room.Id,
            name = room.Name,
            area = room.Area ?? "",
            description = room.Description,
            environment = room.GetProperty<string?>("terrain") ?? "",
            weatherExposed = room.WeatherExposed,
            timeExposed = room.TimeExposed,
            exits,
            doors = doors.Count > 0 ? (object)doors : null,
        };
    }

    private void SendRoomNearby(Guid entityId, Entity entity)
    {
        if (entity.LocationRoomId == null) { return; }
        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null) { return; }
        _connectionManager.Send(entityId, "Room.Nearby", BuildNearbyPayload(room, entity));
    }

    private void SendRoomNearbyToConnection(string connectionId, Entity entity)
    {
        if (entity.LocationRoomId == null) { return; }
        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null) { return; }
        _connectionManager.Send(connectionId, "Room.Nearby", BuildNearbyPayload(room, entity));
    }

    private static object BuildNearbyPayload(Room room, Entity entity)
    {
        var entities = room.Entities
            .Where(e => e.Id != entity.Id)
            .Where(e => e.Type is "player" or "npc" or "mob")
            .Select(e =>
            {
                var templateId = e.GetProperty<string?>("template_id");
                var tags = e.Tags.ToArray();
                var tier = Tapestry.Engine.Combat.HealthTier.Get(e.Stats.Hp, e.Stats.MaxHp);
                return new { name = e.Name, type = e.Type, templateId, tags, healthTier = tier.Tier };
            })
            .ToList();

        return new { entities };
    }
}
