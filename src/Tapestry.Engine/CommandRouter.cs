using Tapestry.Engine.Combat;
using Tapestry.Shared;

namespace Tapestry.Engine;

public class CommandRouter
{
    // Roles that describe the kind of actor invoking a command rather than a privilege
    // the actor must hold. These never gate dispatch.
    private static readonly HashSet<string> ActorTypeRoles =
        new(["player", "mob"], StringComparer.OrdinalIgnoreCase);

    private readonly CommandRegistry _registry;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly BadInputTracker? _badInputTracker;
    private readonly SwellClockManager? _swellClock;

    public CommandRouter(CommandRegistry registry, SessionManager sessions, World world,
        BadInputTracker? badInputTracker = null, SwellClockManager? swellClock = null)
    {
        _registry = registry;
        _sessions = sessions;
        _world = world;
        _badInputTracker = badInputTracker;
        _swellClock = swellClock;
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

        // 'player' and 'mob' are actor-type roles (they describe who/what may invoke a
        // command), not privilege grants. Only genuine privilege roles (admin, builder, ...)
        // gate dispatch. Sweeping 'mob' into the guard set breaks every command a mob can
        // also perform — get/drop/say/etc. — for normal players who lack the 'mob' role.
        var guardedRoles = registration.Roles
            .Where(r => !ActorTypeRoles.Contains(r))
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

        if (registration.Pace == Pace.Battle && _swellClock != null
            && _swellClock.IsActorInActiveSwell(ctx.PlayerEntityId))
        {
            // battle-pace commands are intercepted only when a swell is active for this actor.
            // Pass registration.Keyword (the canonical verb), not ctx.Command which may be
            // an abbreviation - the validator compares against RequiredCounter exactly.
            if (_swellClock.IsKnownCounterVerb(ctx.PlayerEntityId, registration.Keyword))
            {
                // Counter verb: attempt to commit the beat (validated at Resolve).
                var commit = _swellClock.TryCommit(ctx.PlayerEntityId, registration.Keyword);
                switch (commit)
                {
                    case SwellCommitResult.Accepted:
                        break;
                    case SwellCommitResult.NotInWindow:
                        _sessions.SendToPlayer(ctx.PlayerEntityId, "Too soon - there's no opening yet.\r\n");
                        break;
                    case SwellCommitResult.AlreadyCommitted:
                        _sessions.SendToPlayer(ctx.PlayerEntityId, "You've already committed this beat.\r\n");
                        break;
                }
            }
            else
            {
                // reserved: immediate/detection actions (detect-magic, stance-check) run here without committing - detection economy slice
                // Non-counter battle verb during an active swell: blocked.
                _sessions.SendToPlayer(ctx.PlayerEntityId, "The world has slowed. Read the swell.\r\n");
            }
            return;
        }
        // NOT in active swell (or no swell clock): fall through to handler (DPS fires normally at Baseline).

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

    /// <summary>
    /// Parses one raw input line into routable contexts exactly as the game loop does for
    /// drained session input: semicolon chaining + repeat expansion (CommandInputParser),
    /// then single-character alias expansion ('hello -> ' hello). This is the single parse
    /// seam -- both the game loop's input drain and admin.executeAs (the force/at seam)
    /// build their CommandContexts here.
    /// </summary>
    public IReadOnlyList<CommandContext> ParseInput(Guid playerEntityId, string input, bool isChargen = false)
    {
        var parsed = CommandInputParser.Parse(input);
        var contexts = new List<CommandContext>(parsed.Count);
        foreach (var cmd in parsed)
        {
            var verb = cmd.Verb;
            var args = cmd.Args;

            // Preserve alias expansion: single-char non-alphanumeric prefix that resolves as command
            if (verb.Length > 1 && !char.IsLetterOrDigit(verb[0]))
            {
                var alias = verb[0].ToString();
                if (Resolve(alias) != null)
                {
                    args = new[] { verb[1..] }.Concat(args).ToArray();
                    verb = alias;
                }
            }

            contexts.Add(new CommandContext
            {
                PlayerEntityId = playerEntityId,
                RawInput = input,
                Command = verb,
                Args = args,
                IsChargen = isChargen
            });
        }
        return contexts;
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
