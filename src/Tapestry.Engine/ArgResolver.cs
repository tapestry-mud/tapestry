using Microsoft.Extensions.Logging;
using Tapestry.Engine.Registration;
using Tapestry.Shared;

namespace Tapestry.Engine;

public class ArgResolver
{
    private readonly World _world;
    private readonly VisibilityFilter _visibility;
    private readonly DoorService _doors;
    private readonly ILogger<ArgResolver> _logger;
    private readonly RegistrationGate? _gate;

    private readonly Dictionary<string, Func<ActorContext, ArgDefinition, string, (bool, object?, string?)>> _engineTypes;
    private readonly Dictionary<string, Func<ActorContext, ArgDefinition, string, (bool, object?, string?)>> _packTypes = new(StringComparer.OrdinalIgnoreCase);

    public ArgResolver(World world, VisibilityFilter visibility, DoorService doors, ILogger<ArgResolver> logger,
        RegistrationGate? gate = null)
    {
        _world = world;
        _visibility = visibility;
        _doors = doors;
        _logger = logger;
        _gate = gate;
        _engineTypes = BuildEngineTypes();
    }

    private Dictionary<string, Func<ActorContext, ArgDefinition, string, (bool, object?, string?)>> BuildEngineTypes()
    {
        return new Dictionary<string, Func<ActorContext, ArgDefinition, string, (bool, object?, string?)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = (actor, def, token) => (true, token, null),
            ["text"] = (actor, def, token) => (true, token, null),
            ["number"] = (actor, def, token) => ResolveNumber(token),
            ["inventory"] = (actor, def, token) => ResolveInventory(actor, def, token),
            ["room_item"] = (actor, def, token) => ResolveRoomItem(actor, def, token),
            ["entity"] = (actor, def, token) => ResolveEntity(actor, def, token, includeNpcs: true, includePlayers: true),
            ["player"] = (actor, def, token) => ResolveEntity(actor, def, token, includeNpcs: false, includePlayers: true),
            ["npc"] = (actor, def, token) => ResolveEntity(actor, def, token, includeNpcs: true, includePlayers: false),
            ["container"] = (actor, def, token) => ResolveContainer(actor, def, token),
            ["visible"] = (actor, def, token) => ResolveVisible(actor, def, token),
            ["findable"] = (actor, def, token) => ResolveFindable(actor, def, token),
            ["door"] = (actor, def, token) => ResolveDoor(actor, def, token),
        };
    }

    public void RegisterPackType(string name, string owner, Func<ActorContext, ArgDefinition, string, (bool, object?, string?)> resolver)
    {
        _gate?.AssertCommitScope("arg-type", name);
        if (_engineTypes.ContainsKey(name))
        {
            _logger.LogWarning("Pack '{Owner}' attempted to override engine arg type '{Name}' -- ignored.", owner, name);
            return;
        }
        if (_packTypes.ContainsKey(name))
        {
            // The RegistrationPolicy seal barrier now makes a genuine cross-pack collision a
            // boot error before this point; a surviving duplicate here is same-owner re-register.
            _logger.LogWarning("Pack '{Owner}' re-registered arg type '{Name}' -- last registration wins.", owner, name);
        }
        _packTypes[name] = resolver;
    }

    public static bool MatchesInput(Entity entity, string token)
    {
        return entity.HasKeyword(token) || entity.Name.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    public (bool Success, object? Value, string? Error) ResolveToken(
        ActorContext actor, string argName, ArgDefinition def, string token)
    {
        if (_engineTypes.TryGetValue(def.Type, out var engineResolver))
        {
            return engineResolver(actor, def, token);
        }
        if (_packTypes.TryGetValue(def.Type, out var packResolver))
        {
            return packResolver(actor, def, token);
        }
        _logger.LogWarning("Unknown arg type '{Type}' -- falling back to keyword passthrough.", def.Type);
        return (true, token, null);
    }

    public (bool Success, Dictionary<string, object?> Resolved, string? Error) Resolve(
        ActorContext actor,
        Dictionary<string, ArgDefinition> argDefs,
        string[] rawArgs,
        Action<string>? sendError)
    {
        var resolved = new Dictionary<string, object?>();
        var tokenIndex = 0;

        foreach (var (argName, def) in argDefs)
        {
            if (def.Prepositions.Length > 0 && tokenIndex < rawArgs.Length)
            {
                if (def.Prepositions.Contains(rawArgs[tokenIndex], StringComparer.OrdinalIgnoreCase))
                {
                    tokenIndex++;
                }
            }

            if (def.Type == "text")
            {
                if (tokenIndex >= rawArgs.Length)
                {
                    if (def.Required)
                    {
                        sendError?.Invoke($"What {argName}?\r\n");
                        return (false, resolved, $"missing required arg: {argName}");
                    }
                    resolved[argName] = null;
                    continue;
                }
                resolved[argName] = string.Join(" ", rawArgs[tokenIndex..]);
                tokenIndex = rawArgs.Length;
                continue;
            }

            if (tokenIndex >= rawArgs.Length)
            {
                if (def.Required)
                {
                    sendError?.Invoke($"What {argName}?\r\n");
                    return (false, resolved, $"missing required arg: {argName}");
                }
                resolved[argName] = null;
                continue;
            }

            var token = rawArgs[tokenIndex];
            tokenIndex++;

            var (argSuccess, argValue, argError) = ResolveToken(actor, argName, def, token);
            if (!argSuccess && def.Required)
            {
                sendError?.Invoke(argError ?? $"Can't resolve {argName}.\r\n");
                return (false, resolved, argError);
            }

            resolved[argName] = argSuccess ? argValue : null;
        }

        return (true, resolved, null);
    }

    private static (bool, object?, string?) ResolveNumber(string token)
    {
        if (int.TryParse(token, out var n))
        {
            return (true, n, null);
        }
        return (false, null, "That's not a number.\r\n");
    }

    private (bool, object?, string?) ResolveInventory(ActorContext actor, ArgDefinition def, string token)
    {
        if (def.Bulk && (token == "all" || token.StartsWith("all.", StringComparison.OrdinalIgnoreCase)))
        {
            var actorEntity = _world.GetEntity(actor.EntityId);
            if (actorEntity == null)
            {
                return (false, null, "You aren't carrying anything.\r\n");
            }

            var keyword = token.Length > 4 ? token[4..] : null;
            var bulk = actorEntity.Contents
                .Where(e => keyword == null || MatchesInput(e, keyword))
                .Select(ToItemObj)
                .ToArray();
            return bulk.Length > 0 ? (true, bulk, null) : (false, null, "You aren't carrying that.\r\n");
        }

        var (ordinal, name) = ParseOrdinal(token);
        var entity = _world.GetEntity(actor.EntityId);
        if (entity == null)
        {
            return (false, null, "You aren't carrying that.\r\n");
        }

        var match = entity.Contents
            .Where(e => MatchesInput(e, name))
            .Skip(ordinal - 1)
            .FirstOrDefault();

        return match != null ? (true, ToItemObj(match), null) : (false, null, "You aren't carrying that.\r\n");
    }

    private (bool, object?, string?) ResolveRoomItem(ActorContext actor, ArgDefinition def, string token)
    {
        if (actor.RoomId == null)
        {
            return (false, null, "You aren't anywhere.\r\n");
        }
        var room = _world.GetRoom(actor.RoomId);
        if (room == null)
        {
            return (false, null, "You don't see that here.\r\n");
        }

        var actorEntity = _world.GetEntity(actor.EntityId);
        var candidates = def.BypassVisibility
            ? room.Entities.Where(e => e.Type != EntityTypes.Player && e.Type != EntityTypes.Npc)
            : _visibility.GetVisibleEntities(room, actorEntity).Where(e => e.Type != EntityTypes.Player && e.Type != EntityTypes.Npc);

        if (def.Bulk && (token == "all" || token.StartsWith("all.", StringComparison.OrdinalIgnoreCase)))
        {
            var keyword = token.Length > 4 ? token[4..] : null;
            var bulk = candidates
                .Where(e => keyword == null || MatchesInput(e, keyword))
                .Select(ToItemObj)
                .ToArray();
            return bulk.Length > 0 ? (true, bulk, null) : (false, null, "You don't see that here.\r\n");
        }

        var (ordinal, name) = ParseOrdinal(token);
        var roomMatch = candidates
            .Where(e => MatchesInput(e, name))
            .Skip(ordinal - 1)
            .FirstOrDefault();

        return roomMatch != null ? (true, ToItemObj(roomMatch), null) : (false, null, "You don't see that here.\r\n");
    }

    private (bool, object?, string?) ResolveEntity(
        ActorContext actor, ArgDefinition def, string token,
        bool includeNpcs, bool includePlayers)
    {
        if (actor.RoomId == null)
        {
            return (false, null, "You don't see them here.\r\n");
        }
        var room = _world.GetRoom(actor.RoomId);
        if (room == null)
        {
            return (false, null, "You don't see them here.\r\n");
        }

        var (ordinal, name) = ParseOrdinal(token);
        var actorEntity = _world.GetEntity(actor.EntityId);

        var candidates = def.BypassVisibility
            ? room.Entities.AsEnumerable()
            : _visibility.GetVisibleEntities(room, actorEntity);

        var match = candidates
            .Where(e => e.Id != actor.EntityId)
            .Where(e => (includeNpcs && e.Type == EntityTypes.Npc) || (includePlayers && e.Type == EntityTypes.Player))
            .Where(e => MatchesInput(e, name))
            .Skip(ordinal - 1)
            .FirstOrDefault();

        if (match == null)
        {
            return (false, null, "You don't see them here.\r\n");
        }

        return (true, new Dictionary<string, object?>
        {
            ["id"] = match.Id.ToString(),
            ["name"] = match.Name,
            ["type"] = match.Type
        }, null);
    }

    private (bool, object?, string?) ResolveContainer(ActorContext actor, ArgDefinition def, string token)
    {
        var (ordinal, name) = ParseOrdinal(token);

        var actorEntity = _world.GetEntity(actor.EntityId);
        if (actorEntity != null)
        {
            var invMatch = actorEntity.Contents
                .Where(e => e.Type == EntityTypes.Container)
                .Where(e => MatchesInput(e, name))
                .Skip(ordinal - 1)
                .FirstOrDefault();
            if (invMatch != null)
            {
                return (true, ToItemObj(invMatch), null);
            }
        }

        if (actor.RoomId != null)
        {
            var room = _world.GetRoom(actor.RoomId);
            if (room != null)
            {
                var candidates = def.BypassVisibility
                    ? room.Entities.Where(e => e.Type == EntityTypes.Container)
                    : _visibility.GetVisibleEntities(room, actorEntity).Where(e => e.Type == EntityTypes.Container);

                var roomMatch = candidates
                    .Where(e => MatchesInput(e, name))
                    .Skip(ordinal - 1)
                    .FirstOrDefault();
                if (roomMatch != null)
                {
                    return (true, ToItemObj(roomMatch), null);
                }
            }
        }

        return (false, null, "You don't see that container here.\r\n");
    }

    private (bool, object?, string?) ResolveVisible(ActorContext actor, ArgDefinition def, string token)
    {
        var (ordinal, name) = ParseOrdinal(token);
        var actorEntity = _world.GetEntity(actor.EntityId);

        if (name.Equals("self", StringComparison.OrdinalIgnoreCase) || name.Equals("me", StringComparison.OrdinalIgnoreCase))
        {
            if (actorEntity != null)
            {
                return (true, ToVisibleObj(actorEntity, "room"), null);
            }
        }

        if (actorEntity != null)
        {
            var invMatch = actorEntity.Contents
                .Where(e => MatchesInput(e, name))
                .Skip(ordinal - 1)
                .FirstOrDefault();
            if (invMatch != null)
            {
                return (true, ToVisibleObj(invMatch, "inventory"), null);
            }
        }

        if (actor.RoomId != null)
        {
            var room = _world.GetRoom(actor.RoomId);
            if (room != null)
            {
                var candidates = def.BypassVisibility
                    ? room.Entities.AsEnumerable()
                    : _visibility.GetVisibleEntities(room, actorEntity);

                var roomMatch = candidates
                    .Where(e => MatchesInput(e, name))
                    .Skip(ordinal - 1)
                    .FirstOrDefault();
                if (roomMatch != null)
                {
                    return (true, ToVisibleObj(roomMatch, "room"), null);
                }
            }
        }

        return (false, null, "You don't see that here.\r\n");
    }

    private (bool, object?, string?) ResolveFindable(ActorContext actor, ArgDefinition def, string token)
    {
        var (ordinal, name) = ParseOrdinal(token);
        var actorEntity = _world.GetEntity(actor.EntityId);

        if (actorEntity != null)
        {
            var invMatch = actorEntity.Contents
                .Where(e => MatchesInput(e, name))
                .Skip(ordinal - 1)
                .FirstOrDefault();
            if (invMatch != null)
            {
                return (true, ToItemObj(invMatch), null);
            }
        }

        if (actor.RoomId != null)
        {
            var room = _world.GetRoom(actor.RoomId);
            if (room != null)
            {
                var candidates = def.BypassVisibility
                    ? room.Entities.Where(e => e.Type != EntityTypes.Player && e.Type != EntityTypes.Npc)
                    : _visibility.GetVisibleEntities(room, actorEntity).Where(e => e.Type != EntityTypes.Player && e.Type != EntityTypes.Npc);

                var roomMatch = candidates
                    .Where(e => MatchesInput(e, name))
                    .Skip(ordinal - 1)
                    .FirstOrDefault();
                if (roomMatch != null)
                {
                    return (true, ToItemObj(roomMatch), null);
                }
            }
        }

        return (false, null, "You don't see that here.\r\n");
    }

    private (bool, object?, string?) ResolveDoor(ActorContext actor, ArgDefinition def, string token)
    {
        if (actor.RoomId == null)
        {
            return (false, null, "You aren't anywhere.\r\n");
        }
        var room = _world.GetRoom(actor.RoomId);
        if (room == null)
        {
            return (false, null, "There's no door here.\r\n");
        }

        var dir = _doors.ResolveTarget(room, token);
        if (dir == null)
        {
            return (false, null, "You don't see a door that way.\r\n");
        }

        var door = _doors.GetDoor(room, dir.Value);
        if (door == null)
        {
            return (false, null, "There's no door there.\r\n");
        }

        return (true, new Dictionary<string, object?>
        {
            ["direction"] = dir.Value.ToShortString(),
            ["door"] = new Dictionary<string, object?>
            {
                ["name"] = door.Name,
                ["isClosed"] = door.IsClosed,
                ["isLocked"] = door.IsLocked,
                ["keyId"] = door.KeyId
            }
        }, null);
    }

    private static (int Ordinal, string Name) ParseOrdinal(string token)
    {
        var dot = token.IndexOf('.');
        if (dot > 0 && int.TryParse(token[..dot], out var n) && n >= 1)
        {
            return (n, token[(dot + 1)..]);
        }
        return (1, token);
    }

    private static Dictionary<string, object?> ToItemObj(Entity e)
    {
        var nameWords = e.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .ToHashSet();
        var keyword = e.Keywords.FirstOrDefault(k => nameWords.Contains(k.ToLowerInvariant()))
            ?? e.Keywords.FirstOrDefault()
            ?? e.Name.Split(' ').LastOrDefault()
            ?? e.Name;
        return new Dictionary<string, object?>
        {
            ["id"] = e.Id.ToString(),
            ["name"] = e.Name,
            ["keyword"] = keyword,
            ["templateId"] = e.GetProperty<string>(CommonProperties.TemplateId)
        };
    }

    private static Dictionary<string, object?> ToVisibleObj(Entity e, string source)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = e.Id.ToString(),
            ["name"] = e.Name,
            ["type"] = e.Type,
            ["source"] = source
        };
    }
}
