// src/Tapestry.Engine/ArgResolver.cs
using Tapestry.Shared;

namespace Tapestry.Engine;

public static class ArgResolver
{
    // Returns (success, resolvedArgs, errorMessage)
    // resolvedArgs keys match argDefs keys. Value is null for optional args that didn't resolve.
    public static (bool Success, Dictionary<string, object?> Resolved, string? Error) Resolve(
        ActorContext actor,
        Dictionary<string, ArgDefinition> argDefs,
        string[] rawArgs,
        World world,
        Action<string>? sendError)
    {
        var resolved = new Dictionary<string, object?>();
        var tokenIndex = 0;

        foreach (var (argName, def) in argDefs)
        {
            // Strip prepositions
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

            var (argSuccess, argValue, argError) = ResolveToken(actor, argName, def, token, world);
            if (!argSuccess && def.Required)
            {
                sendError?.Invoke(argError ?? $"Can't resolve {argName}.\r\n");
                return (false, resolved, argError);
            }

            resolved[argName] = argValue;
        }

        return (true, resolved, null);
    }

    private static (bool Success, object? Value, string? Error) ResolveToken(
        ActorContext actor, string argName, ArgDefinition def, string token, World world)
    {
        return def.Type switch
        {
            "keyword" => (true, token, null),
            "number" => ResolveNumber(token),
            "inventory" => ResolveInventory(actor, def, token, world),
            "room_item" => ResolveRoomItem(actor, def, token, world),
            "entity" => ResolveEntity(actor, def, token, world, includeNpcs: true, includePlayers: true),
            "player" => ResolveEntity(actor, def, token, world, includeNpcs: false, includePlayers: true),
            "npc" => ResolveEntity(actor, def, token, world, includeNpcs: true, includePlayers: false),
            "container" => ResolveContainer(actor, token, world),
            _ => (true, token, null)
        };
    }

    private static (bool, object?, string?) ResolveNumber(string token)
    {
        if (int.TryParse(token, out var n))
        {
            return (true, n, null);
        }
        return (false, null, "That's not a number.\r\n");
    }

    private static (bool, object?, string?) ResolveInventory(ActorContext actor, ArgDefinition def, string token, World world)
    {
        if (def.Bulk && (token == "all" || token.StartsWith("all.", StringComparison.OrdinalIgnoreCase)))
        {
            var actorEntity = world.GetEntity(actor.EntityId);
            if (actorEntity == null)
            {
                return (false, null, "You aren't carrying anything.\r\n");
            }

            var keyword = token.Length > 4 ? token[4..] : null;
            var bulk = actorEntity.Contents
                .Where(e => keyword == null || e.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Select(ToItemObj)
                .ToArray();
            return bulk.Length > 0 ? (true, bulk, null) : (false, null, "You aren't carrying that.\r\n");
        }

        var (ordinal, name) = ParseOrdinal(token);
        var entity = world.GetEntity(actor.EntityId);
        if (entity == null)
        {
            return (false, null, "You aren't carrying that.\r\n");
        }

        var match = entity.Contents
            .Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Skip(ordinal - 1)
            .FirstOrDefault();

        return match != null ? (true, ToItemObj(match), null) : (false, null, "You aren't carrying that.\r\n");
    }

    private static (bool, object?, string?) ResolveRoomItem(ActorContext actor, ArgDefinition def, string token, World world)
    {
        if (actor.RoomId == null)
        {
            return (false, null, "You aren't anywhere.\r\n");
        }
        var room = world.GetRoom(actor.RoomId);
        if (room == null)
        {
            return (false, null, "You don't see that here.\r\n");
        }

        if (def.Bulk && (token == "all" || token.StartsWith("all.", StringComparison.OrdinalIgnoreCase)))
        {
            var keyword = token.Length > 4 ? token[4..] : null;
            var bulk = room.Entities
                .Where(e => e.Type != "player" && e.Type != "npc")
                .Where(e => keyword == null || e.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Select(ToItemObj)
                .ToArray();
            return bulk.Length > 0 ? (true, bulk, null) : (false, null, "You don't see that here.\r\n");
        }

        var (ordinal, name) = ParseOrdinal(token);
        var match = room.Entities
            .Where(e => e.Type != "player" && e.Type != "npc")
            .Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Skip(ordinal - 1)
            .FirstOrDefault();

        return match != null ? (true, ToItemObj(match), null) : (false, null, "You don't see that here.\r\n");
    }

    private static (bool, object?, string?) ResolveEntity(
        ActorContext actor, ArgDefinition def, string token, World world,
        bool includeNpcs, bool includePlayers)
    {
        if (actor.RoomId == null)
        {
            return (false, null, "You don't see them here.\r\n");
        }
        var room = world.GetRoom(actor.RoomId);
        if (room == null)
        {
            return (false, null, "You don't see them here.\r\n");
        }

        var (ordinal, name) = ParseOrdinal(token);

        var match = room.Entities
            .Where(e => e.Id != actor.EntityId)
            .Where(e => (includeNpcs && e.Type == "npc") || (includePlayers && e.Type == "player"))
            .Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
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
            ["type"] = match.Type == "npc" ? "npc" : "player"
        }, null);
    }

    // Pre-Flight Fix 2: container resolver
    private static (bool, object?, string?) ResolveContainer(ActorContext actor, string token, World world)
    {
        var (ordinal, name) = ParseOrdinal(token);

        // Search actor inventory first
        var actorEntity = world.GetEntity(actor.EntityId);
        if (actorEntity != null)
        {
            var invMatch = actorEntity.Contents
                .Where(e => e.HasTag("container"))
                .Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                .Skip(ordinal - 1)
                .FirstOrDefault();
            if (invMatch != null)
            {
                return (true, ToItemObj(invMatch), null);
            }
        }

        // Then room
        if (actor.RoomId != null)
        {
            var room = world.GetRoom(actor.RoomId);
            if (room != null)
            {
                var roomMatch = room.Entities
                    .Where(e => e.HasTag("container"))
                    .Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
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
}
