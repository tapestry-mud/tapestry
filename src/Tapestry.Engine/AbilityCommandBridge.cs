using Tapestry.Engine.Abilities;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Registration;
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
    private readonly RegistrationGate? _gate;

    public AbilityCommandBridge(
        AbilityRegistry abilities,
        ProficiencyManager proficiency,
        CommandRegistry commands,
        World world,
        CombatManager combat,
        SessionManager sessions,
        GameLoop gameLoop,
        RegistrationGate? gate = null)
    {
        _abilities = abilities;
        _proficiency = proficiency;
        _commands = commands;
        _world = world;
        _combat = combat;
        _sessions = sessions;
        _gameLoop = gameLoop;
        _gate = gate;
    }

    public void WireAll()
    {
        // Kernel-sanctioned post-seal write scope, NOT a RegistrationPolicy route. The
        // bridge's auto-generated commands intentionally COEXIST with same-keyword pack
        // commands and lose to them by priority (the documented shadowing seam above --
        // e.g. @tapestry/core registers a priority-1 `rescue` command shadowing the
        // bridge's `rescue` ability command). Routing these Records through the policy
        // would turn that sanctioned shadowing into a collision boot error on the live
        // corpus, so the bridge writes directly inside an explicit commit scope instead.
        using var scope = _gate?.EnterCommitScope();
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
            actorCtx => { ExecuteAbilityCommandForActor(actorCtx, abilityId, displayName); },
            aliases: aliases,
            priority: 0,
            packName: ability.PackName,
            sourceFile: ability.SourceFile,
            visibleTo: visibleTo,
            roles: ["player", "mob"],
            pace: Pace.Battle
        );
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
            if (actorCtx.Source == "player" && _combat.HasFleeCooldown(entity.Id, _gameLoop.TickCount))
            {
                _sessions.SendToPlayer(actorCtx.EntityId, "You're too winded from fleeing to attack right now.\r\n");
                return;
            }
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
                    .Where(e => e.Id != entity.Id && (e.Type == EntityTypes.Npc || e.Type == EntityTypes.Player))
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
