using Tapestry.Engine.Abilities;
using Tapestry.Engine.Combat;
using Tapestry.Shared;

namespace Tapestry.Engine;


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
    private readonly GameLoop _gameLoop;

    public AbilityCommandBridge(
        AbilityRegistry abilities,
        ProficiencyManager proficiency,
        CommandRegistry commands,
        World world,
        CombatManager combat,
        SessionManager sessions,
        GameLoop gameLoop)
    {
        _abilities = abilities;
        _proficiency = proficiency;
        _commands = commands;
        _world = world;
        _combat = combat;
        _sessions = sessions;
        _gameLoop = gameLoop;
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

        // command_name is the explicit, intentional keyword set by the pack author.
        // Falls back to the short ID (after last ':') if not set.
        var colonPos = abilityId.LastIndexOf(':');
        var shortId = colonPos >= 0 ? abilityId[(colonPos + 1)..] : abilityId;
        var keyword = ability.CommandName ?? shortId;
        var aliases = keyword != abilityId ? new[] { abilityId } : Array.Empty<string>();

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
            visibleTo: visibleTo,
            roles: ["player", "mob"],
            actorHandler: actorCtx => { ExecuteAbilityCommandForActor(actorCtx, abilityId, displayName); }
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
            var engaged = _combat.Engage(player, targetEntity, _gameLoop.TickCount);
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

    private void ExecuteAbilityCommandForActor(ActorContext actorCtx, string abilityId, string displayName)
    {
        var entity = _world.GetEntity(actorCtx.EntityId);
        if (entity == null) { return; }

        var proficiency = _proficiency.GetProficiency(entity.Id, abilityId);
        if (!proficiency.HasValue || proficiency.Value <= 0)
        {
            if (actorCtx.Source == "player")
            {
                _sessions.SendToPlayer(actorCtx.EntityId, $"You don't know how to {displayName}.\r\n");
            }
            return;
        }

        var targetId = ResolveTargetForActor(actorCtx, entity, abilityId, displayName);
        if (targetId == null) { return; }

        var targetEntity = _world.GetEntity(targetId.Value);
        if (targetEntity == null) { return; }

        if (targetEntity.Id != entity.Id && !_combat.IsInCombat(entity.Id))
        {
            var engaged = _combat.Engage(entity, targetEntity, _gameLoop.TickCount);
            if (!engaged)
            {
                if (actorCtx.Source == "player")
                {
                    _sessions.SendToPlayer(actorCtx.EntityId, "You can't attack that.\r\n");
                }
                return;
            }
        }

        var queue = entity.GetProperty<List<object>>(AbilityProperties.QueuedActions) ?? new List<object>();
        queue.Add(new Dictionary<string, object?>
        {
            ["abilityId"] = abilityId,
            ["targetEntityId"] = targetId.Value.ToString()
        });
        entity.SetProperty(AbilityProperties.QueuedActions, queue);
    }

    private Guid? ResolveTargetForActor(ActorContext actorCtx, Entity entity, string abilityId, string displayName)
    {
        if (actorCtx.RawArgs.Length > 0)
        {
            var raw = string.Join(" ", actorCtx.RawArgs).ToLower();

            if (raw == "self" || raw == "me" || raw == entity.Name.ToLower()) { return entity.Id; }

            var targetName = raw;
            var targetIndex = 1;
            var dotPos = raw.IndexOf('.');
            if (dotPos > 0 && int.TryParse(raw[..dotPos], out var parsedIndex))
            {
                targetIndex = parsedIndex;
                targetName = raw[(dotPos + 1)..];
            }

            if (actorCtx.RoomId != null)
            {
                var matches = _world.GetEntitiesInRoom(actorCtx.RoomId)
                    .Where(e => e.Id != entity.Id && (e.HasTag("npc") || e.HasTag("player")))
                    .Where(e => e.Name.ToLower().Contains(targetName))
                    .ToList();
                if (matches.Count >= targetIndex) { return matches[targetIndex - 1].Id; }
            }

            if (_combat.IsInCombat(entity.Id)) { return _combat.GetPrimaryTarget(entity.Id); }

            if (actorCtx.Source == "player")
            {
                _sessions.SendToPlayer(actorCtx.EntityId, "You don't see that here.\r\n");
            }
            return null;
        }

        if (_combat.IsInCombat(entity.Id)) { return _combat.GetPrimaryTarget(entity.Id); }

        var ability = _abilities.Get(abilityId);
        if (ability != null && ability.CanTarget.Contains("self")) { return entity.Id; }

        if (actorCtx.Source == "player")
        {
            _sessions.SendToPlayer(actorCtx.EntityId, $"Use {displayName} on whom?\r\n");
        }
        return null;
    }
}
