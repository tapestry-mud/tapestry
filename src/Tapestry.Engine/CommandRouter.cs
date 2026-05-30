using Tapestry.Shared;

namespace Tapestry.Engine;

public class CommandRouter
{
    private readonly CommandRegistry _registry;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly BadInputTracker? _badInputTracker;

    public CommandRouter(CommandRegistry registry, SessionManager sessions, World world,
        BadInputTracker? badInputTracker = null)
    {
        _registry = registry;
        _sessions = sessions;
        _world = world;
        _badInputTracker = badInputTracker;
    }

    public void Route(CommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Command))
        {
            return;
        }

        var registration = _registry.Resolve(ctx.Command, "player");
        if (registration == null)
        {
            if (_badInputTracker != null)
            {
                var entity = _world.GetEntity(ctx.PlayerEntityId);
                _badInputTracker.Record(ctx.Command, ctx.RawInput, entity?.Name ?? "", entity?.LocationRoomId);
            }
            _sessions.SendToPlayer(ctx.PlayerEntityId, "Huh?\r\n");
            return;
        }

        var guardedRoles = registration.Roles
            .Where(r => !string.Equals(r, "player", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (guardedRoles.Length > 0)
        {
            var entity = _world.GetEntity(ctx.PlayerEntityId);
            var hasAny = entity != null && guardedRoles.Any(role => entity.HasRole(role));
            if (!hasAny)
            {
                _sessions.SendToPlayer(ctx.PlayerEntityId, "Huh?\r\n");
                return;
            }
        }

        registration.ActorHandler(BuildPlayerActorContext(ctx));
    }

    public void RouteForMob(Guid entityId, string commandStr, string? roomId, string name)
    {
        var parts = commandStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        var verb = parts[0];
        var args = parts.Length > 1 ? parts[1..] : [];

        var registration = _registry.Resolve(verb, "mob");
        if (registration == null)
        {
            return;
        }

        var actorCtx = new ActorContext
        {
            EntityId = entityId,
            Name = name,
            RoomId = roomId,
            Source = "mob",
            RawInput = commandStr,
            Command = verb,
            RawArgs = args
        };

        registration.ActorHandler(actorCtx);
    }

    public CommandRegistration? Resolve(string keyword)
    {
        return _registry.Resolve(keyword);
    }

    private ActorContext BuildPlayerActorContext(CommandContext ctx)
    {
        var entity = _world.GetEntity(ctx.PlayerEntityId);
        return new ActorContext
        {
            EntityId = ctx.PlayerEntityId,
            Name = entity?.Name ?? "",
            RoomId = entity?.LocationRoomId,
            RawInput = ctx.RawInput,
            Command = ctx.Command,
            RawArgs = ctx.Args,
            Source = "player"
        };
    }
}
