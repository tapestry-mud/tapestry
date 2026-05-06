using Tapestry.Engine.Abilities;
using Tapestry.Engine.Combat;
using Tapestry.Shared;

namespace Tapestry.Engine;

file static class AbilityNameWords
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        { "the", "of", "in", "a", "an", "and", "or", "with" };

    public static string[] GetAliases(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => !StopWords.Contains(w))
            .ToArray();

    // Strip leading args that are words from the ability name so "heron sword form trolloc" resolves to "trolloc".
    public static string[] StripNamePrefix(string[] args, string abilityName)
    {
        var nameWords = new HashSet<string>(
            abilityName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        var i = 0;
        while (i < args.Length - 1 && nameWords.Contains(args[i]))
        {
            i++;
        }
        return i > 0 ? args[i..] : args;
    }
}

/// <summary>
/// Wires every active ability as a directly typeable command after packs finish loading.
/// Commands are registered once; visibility is dynamic via the VisibleTo predicate,
/// which re-checks proficiency on each call. Handlers also re-check proficiency so
/// hidden-but-resolvable commands reject with a friendly message.
/// Pack authors who need custom logic register a command at priority > 0 to shadow
/// the auto-generated entry.
/// </summary>
public class AbilityCommandBridge
{
    private readonly AbilityRegistry _abilities;
    private readonly ProficiencyManager _proficiency;
    private readonly CommandRegistry _commands;
    private readonly World _world;
    private readonly CombatManager _combat;
    private readonly SessionManager _sessions;

    public AbilityCommandBridge(
        AbilityRegistry abilities,
        ProficiencyManager proficiency,
        CommandRegistry commands,
        World world,
        CombatManager combat,
        SessionManager sessions)
    {
        _abilities = abilities;
        _proficiency = proficiency;
        _commands = commands;
        _world = world;
        _combat = combat;
        _sessions = sessions;
    }

    public void WireAll()
    {
        foreach (var ability in _abilities.GetAll())
        {
            if (ability.Type != AbilityType.Active) { continue; }
            RegisterAbilityCommand(ability);
        }
    }

    private void RegisterAbilityCommand(AbilityDefinition ability)
    {
        var abilityId = ability.Id;
        var displayName = ability.ShortName ?? ability.Name;
        var category = ability.Category == AbilityCategory.Skill ? "skills" : "spells";

        // Primary keyword: first word of the ability name (lowercase).
        // Aliases: all non-stop words from the name + full namespaced ID.
        var nameWords = AbilityNameWords.GetAliases(ability.Name);
        var keyword = nameWords.Length > 0 ? nameWords[0] : abilityId;
        var colonPos = abilityId.LastIndexOf(':');
        var aliases = nameWords.Skip(1)
            .Concat(colonPos >= 0 ? new[] { abilityId } : Array.Empty<string>())
            .Distinct()
            .Where(a => a != keyword)
            .ToArray();

        Func<Entity, bool> visibleTo = entity =>
        {
            var proficiency = _proficiency.GetProficiency(entity.Id, abilityId);
            return proficiency.HasValue && proficiency.Value > 0;
        };

        _commands.Register(
            keyword,
            ctx => { ExecuteAbilityCommand(ctx, abilityId, displayName); },
            aliases: aliases,
            priority: 0,
            packName: ability.PackName,
            description: displayName,
            category: category,
            sourceFile: ability.SourceFile,
            visibleTo: visibleTo
        );
    }

    private void ExecuteAbilityCommand(CommandContext ctx, string abilityId, string displayName)
    {
        var player = _world.GetEntity(ctx.PlayerEntityId);
        if (player == null) { return; }

        var proficiency = _proficiency.GetProficiency(player.Id, abilityId);
        if (!proficiency.HasValue || proficiency.Value <= 0)
        {
            _sessions.SendToPlayer(ctx.PlayerEntityId, $"You don't know how to {displayName}.\r\n");
            return;
        }

        var targetId = ResolveTarget(player, abilityId, ctx.Args);
        if (targetId == null) { return; }

        var targetEntity = _world.GetEntity(targetId.Value);
        if (targetEntity == null) { return; }

        if (targetEntity.Id != player.Id && !_combat.IsInCombat(player.Id))
        {
            var engaged = _combat.Engage(player, targetEntity);
            if (!engaged)
            {
                _sessions.SendToPlayer(ctx.PlayerEntityId, "You can't attack that.\r\n");
                return;
            }
        }

        var queue = player.GetProperty<List<object>>(AbilityProperties.QueuedActions) ?? new List<object>();
        queue.Add(new Dictionary<string, object?>
        {
            ["abilityId"] = abilityId,
            ["targetEntityId"] = targetId.Value.ToString()
        });
        player.SetProperty(AbilityProperties.QueuedActions, queue);
    }

    private Guid? ResolveTarget(Entity player, string abilityId, string[] args)
    {
        if (player.LocationRoomId == null)
        {
            _sessions.SendToPlayer(player.Id, "You can't use abilities here.\r\n");
            return null;
        }

        var ability = _abilities.Get(abilityId);

        if (args.Length > 0)
        {
            // Strip ability name words typed before the target: "heron sword form trolloc" -> "trolloc"
            if (ability != null) { args = AbilityNameWords.StripNamePrefix(args, ability.Name); }
            var raw = string.Join(" ", args).ToLower();

            // self-keywords and player's own name always resolve to self
            if (raw == "self" || raw == "me" || raw == player.Name.ToLower())
            {
                return player.Id;
            }

            // Parse optional index prefix: "2.goblin" targets the second goblin
            var targetName = raw;
            var targetIndex = 1;
            var dotPos = raw.IndexOf('.');
            if (dotPos > 0 && int.TryParse(raw[..dotPos], out var parsedIndex))
            {
                targetIndex = parsedIndex;
                targetName = raw[(dotPos + 1)..];
            }

            var matches = _world.GetEntitiesInRoom(player.LocationRoomId)
                .Where(e => e.Id != player.Id && (e.HasTag("npc") || e.HasTag("player")))
                .Where(e => e.Name.ToLower().Contains(targetName))
                .OrderByDescending(e => e.HasTag("killable") || e.Type == "player" ? 1 : 0)
                .ToList();

            if (matches.Count >= targetIndex)
            {
                return matches[targetIndex - 1].Id;
            }

            // Named target not found — fall back to combat target if in combat
            if (_combat.IsInCombat(player.Id))
            {
                return _combat.GetPrimaryTarget(player.Id);
            }
            _sessions.SendToPlayer(player.Id, "You don't see that here.\r\n");
            return null;
        }

        if (_combat.IsInCombat(player.Id))
        {
            return _combat.GetPrimaryTarget(player.Id);
        }

        if (ability != null && ability.CanTarget.Contains("self"))
        {
            return player.Id;
        }

        var displayName = ability?.ShortName ?? ability?.Name ?? abilityId;
        _sessions.SendToPlayer(player.Id, $"Use {displayName} on whom?\r\n");
        return null;
    }
}
