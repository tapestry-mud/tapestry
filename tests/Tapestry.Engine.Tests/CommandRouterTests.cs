using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class CommandRouterTests
{
    private static BadInputTracker MakeTracker() =>
        new BadInputTracker(Microsoft.Extensions.Logging.Abstractions.NullLogger<BadInputTracker>.Instance, null);

    [Fact]
    public void Route_ParsesCommandAndArgs()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        string? receivedCmd = null;
        string[]? receivedArgs = null;
        registry.Register("say", (ctx) =>
        {
            receivedCmd = ctx.Command;
            receivedArgs = ctx.RawArgs;
        }, packName: "core");
        var router = new CommandRouter(registry, sessions, world, MakeTracker());
        var ctx = MakeContext("say hello world");
        router.Route(ctx);
        receivedCmd.Should().Be("say");
        receivedArgs.Should().BeEquivalentTo(["hello", "world"]);
    }

    [Fact]
    public void Route_UnknownCommand_SendsHuh()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var router = new CommandRouter(registry, sessions, world, MakeTracker());

        var connection = new FakeConnection();
        var entity = new Entity("player", "Test");
        var session = new PlayerSession(connection, entity);
        sessions.Add(session);

        var ctx = MakeContext("xyzzy", entity.Id);
        router.Route(ctx);
        string.Join("", connection.SentText).Should().Contain("Huh?");
    }

    [Fact]
    public void Route_EmptyInput_DoesNothing()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var router = new CommandRouter(registry, sessions, world, MakeTracker());
        var called = false;
        registry.Register("test", (_) => { called = true; }, packName: "core");
        var ctx = MakeContext("");
        router.Route(ctx);
        called.Should().BeFalse();
    }

    [Fact]
    public void Resolve_FindsRegisteredCommand()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        registry.Register("say", (ctx) => { }, packName: "core");
        var router = new CommandRouter(registry, sessions, world, MakeTracker());

        var result = router.Resolve("say");

        result.Should().NotBeNull();
        result!.Keyword.Should().Be("say");
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNotRegistered()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var router = new CommandRouter(registry, sessions, world, MakeTracker());

        var result = router.Resolve("xyzzy");

        result.Should().BeNull();
    }

    [Fact]
    public void RouteForMob_DispatchesMobRoleCommand()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var dispatched = false;
        registry.Register("say", actor => { dispatched = true; }, roles: ["player", "mob"]);

        var router = new CommandRouter(registry, sessions, world, MakeTracker());

        router.RouteForMob(Guid.NewGuid(), "say hello", null, "Goblin");

        dispatched.Should().BeTrue();
    }

    [Fact]
    public void RouteForMob_PlayerOnlyCommand_DoesNotDispatch()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var dispatched = false;
        registry.Register("score", _ => { dispatched = true; }, roles: ["player"]);

        var router = new CommandRouter(registry, sessions, world, MakeTracker());

        router.RouteForMob(Guid.NewGuid(), "score", null, "Goblin");

        dispatched.Should().BeFalse();
    }

    [Fact]
    public void Route_UnknownCommand_RecordsBadInput()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var tracker = MakeTracker();
        var router = new CommandRouter(registry, sessions, world, tracker);

        var connection = new FakeConnection();
        var entity = new Entity("player", "Alice");
        var session = new PlayerSession(connection, entity);
        sessions.Add(session);

        var ctx = MakeContext("xyzzy some args", entity.Id);
        router.Route(ctx);

        tracker.GetEntries().Should().ContainSingle(e => e.Verb == "xyzzy" && e.Count == 1);
    }

    [Fact]
    public void Route_AdminCommandWithoutRole_DoesNotRecordBadInput()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var tracker = MakeTracker();
        var router = new CommandRouter(registry, sessions, world, tracker);

        registry.Register("secret", _ => { }, roles: ["admin"]);

        var connection = new FakeConnection();
        var entity = new Entity("player", "Alice");
        var session = new PlayerSession(connection, entity);
        sessions.Add(session);

        var ctx = MakeContext("secret", entity.Id);
        router.Route(ctx);

        tracker.GetEntries().Should().BeEmpty();
    }

    private static CommandContext MakeContext(string input, Guid? entityId = null)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new CommandContext
        {
            PlayerEntityId = entityId ?? Guid.NewGuid(),
            RawInput = input,
            Command = parts.Length > 0 ? parts[0] : "",
            Args = parts.Length > 1 ? parts[1..] : []
        };
    }

    // --- Swell clock dispatch tests ---

    private static (World world, EventBus eventBus, CombatManager combat,
        WindowValidatorRegistry validators, Room room, Entity player, Entity boss)
        SetupSwellWorld()
    {
        var world = new World();
        var eventBus = new EventBus();
        var combat = new CombatManager(world, eventBus, vitalsService: new VitalsService(eventBus));
        var validators = new WindowValidatorRegistry();
        var room = new Room("core:arena", "Arena", "A test arena.");
        world.AddRoom(room);

        var player = new Entity("player", "Travis");
        player.AddTag("player");
        player.Stats.BaseMaxHp = 100;
        player.Stats.SetVital(VitalKind.Hp, 100);
        room.AddEntity(player);
        world.TrackEntity(player);

        var boss = new Entity("npc", "the swell-warden");
        boss.AddTag("npc");
        boss.Stats.BaseMaxHp = 200;
        boss.Stats.SetVital(VitalKind.Hp, 200);
        boss.SetProperty("swell_window", "telegraph-rung");
        boss.SetProperty("swell_tell", "full");
        boss.SetProperty("swell_baseline_gap_ticks", 2);
        boss.SetProperty("swell_jitter_ticks", 0);
        boss.SetProperty("swell_telegraph_ticks", 2);
        boss.SetProperty("swell_window_ticks", 4);
        // Both lines use "sidestep" as the counter so the required counter is always known.
        boss.SetProperty("swell_line1_id", "sweep");
        boss.SetProperty("swell_line1_counter", "sidestep");
        boss.SetProperty("swell_line1_tell_full", "The warden winds up a wide SWEEP -- sidestep it!");
        boss.SetProperty("swell_line2_id", "crush");
        boss.SetProperty("swell_line2_counter", "sidestep");
        boss.SetProperty("swell_line2_tell_full", "The warden rears for a downward CRUSH -- sidestep it!");
        boss.SetProperty("swell_chunk_pct", 15);
        boss.SetProperty("swell_whiff_pct", 10);
        boss.SetProperty("swell_weather_pct", 8);
        boss.SetProperty("swell_narration_countered", "You read it clean and the warden reels.");
        boss.SetProperty("swell_narration_whiffed", "Wrong read - the blow lands hard.");
        boss.SetProperty("swell_narration_weathered", "You freeze, and the blow lands.");
        room.AddEntity(boss);
        world.TrackEntity(boss);

        return (world, eventBus, combat, validators, room, player, boss);
    }

    private static void RegisterTelegraphRungValidator(WindowValidatorRegistry validators)
    {
        validators.Register("telegraph-rung", ctx =>
        {
            if (string.IsNullOrEmpty(ctx.Command.Verb))
            {
                return new ValidationResult { Outcome = WindowOutcome.Weathered, NarrationKey = "weathered" };
            }
            if (ctx.Command.Verb == ctx.Swell!.RequiredCounter)
            {
                return new ValidationResult { Outcome = WindowOutcome.Countered, NarrationKey = "countered" };
            }
            return new ValidationResult { Outcome = WindowOutcome.Whiffed, NarrationKey = "whiffed" };
        });
    }

    [Fact]
    public void Route_BattleVerb_RoutesToSwellClock_NotHandler()
    {
        var (world, eventBus, combat, validators, _, player, boss) = SetupSwellWorld();
        RegisterTelegraphRungValidator(validators);

        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var connection = new FakeConnection();
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        var clock = new SwellClockManager(world, eventBus, combat, validators, vitalsService: new VitalsService(eventBus));
        // Router constructed WITH the swell clock.
        var router = new CommandRouter(registry, sessions, world, swellClock: clock);

        var handlerRan = false;
        registry.Register("sidestep", _ => { handlerRan = true; }, pace: Pace.Battle);

        combat.Engage(player, boss);
        // Drive to an open Window: baseline ends at tick 2, telegraph ends at tick 4.
        clock.AdvanceAll(0);
        clock.AdvanceAll(2); // -> Telegraph
        clock.AdvanceAll(4); // -> Window

        router.Route(new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "sidestep",
            Command = "sidestep",
            Args = Array.Empty<string>()
        });

        Assert.False(handlerRan); // clock owns the outcome, not the handler
    }

    [Fact]
    public void Route_FreeVerb_DispatchesHandler_AsToday()
    {
        var (world, eventBus, combat, validators, _, player, boss) = SetupSwellWorld();

        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var connection = new FakeConnection();
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        var clock = new SwellClockManager(world, eventBus, combat, validators, vitalsService: new VitalsService(eventBus));
        var router = new CommandRouter(registry, sessions, world, swellClock: clock);

        var ran = false;
        registry.Register("look", _ => { ran = true; }); // default Pace.Free

        combat.Engage(player, boss);
        clock.AdvanceAll(0);
        clock.AdvanceAll(2); // -> Telegraph
        clock.AdvanceAll(4); // -> Window

        router.Route(new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "look",
            Command = "look",
            Args = Array.Empty<string>()
        });

        Assert.True(ran); // free verbs always dispatch immediately
    }

    [Fact]
    public void Route_BattleVerb_Abbreviation_CommitsCanonicalCounter()
    {
        var (world, eventBus, combat, validators, _, player, boss) = SetupSwellWorld();
        RegisterTelegraphRungValidator(validators);

        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var connection = new FakeConnection();
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        var clock = new SwellClockManager(world, eventBus, combat, validators, vitalsService: new VitalsService(eventBus));
        var router = new CommandRouter(registry, sessions, world, swellClock: clock);

        registry.Register("sidestep", _ => { }, pace: Pace.Battle);

        combat.Engage(player, boss);
        clock.AdvanceAll(0);
        clock.AdvanceAll(2); // -> Telegraph
        clock.AdvanceAll(4); // -> Window (both lines use "sidestep" as counter)

        // Player types the abbreviation "side"; registry prefix-matches to "sidestep".
        router.Route(new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "side",
            Command = "side",
            Args = Array.Empty<string>()
        });

        // Resolve: canonical "sidestep" was committed, so boss takes chunk damage (COUNTERED).
        clock.AdvanceAll(5);
        Assert.True(boss.Stats.Hp < boss.Stats.MaxHp); // COUNTERED, boss took the chunk
        Assert.Equal(100, player.Stats.Hp);             // player unharmed
    }

    [Fact]
    public void Route_BattleVerb_AtBaseline_FallsThroughToHandler()
    {
        // Move 2: a pace:battle command with no active swell must run the handler (DPS fires at Baseline).
        var (world, eventBus, combat, validators, _, player, boss) = SetupSwellWorld();
        RegisterTelegraphRungValidator(validators);

        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var connection = new FakeConnection();
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        var clock = new SwellClockManager(world, eventBus, combat, validators, vitalsService: new VitalsService(eventBus));
        var router = new CommandRouter(registry, sessions, world, swellClock: clock);

        var handlerRan = false;
        registry.Register("kick", _ => { handlerRan = true; }, pace: Pace.Battle);

        combat.Engage(player, boss);
        clock.AdvanceAll(0); // Baseline - no active swell

        router.Route(new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "kick",
            Command = "kick",
            Args = Array.Empty<string>()
        });

        Assert.True(handlerRan); // handler must run at Baseline
    }

    [Fact]
    public void Route_BattleVerb_DuringActiveSwell_IsIntercepted()
    {
        // Move 2: a pace:battle command during an active swell is intercepted (handler blocked).
        var (world, eventBus, combat, validators, _, player, boss) = SetupSwellWorld();
        RegisterTelegraphRungValidator(validators);

        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var connection = new FakeConnection();
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        var clock = new SwellClockManager(world, eventBus, combat, validators, vitalsService: new VitalsService(eventBus));
        var router = new CommandRouter(registry, sessions, world, swellClock: clock);

        var handlerRan = false;
        registry.Register("kick", _ => { handlerRan = true; }, pace: Pace.Battle);

        combat.Engage(player, boss);
        clock.AdvanceAll(0);
        clock.AdvanceAll(2); // -> Telegraph (active swell)

        router.Route(new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "kick",
            Command = "kick",
            Args = Array.Empty<string>()
        });

        Assert.False(handlerRan); // handler must NOT run during active swell
    }

    [Fact]
    public void Route_Move4_CounterVerb_DuringWindow_CommitsAndDoesNotRunHandler()
    {
        // Move 4: the known counter verb during Window commits to TryCommit; handler does NOT run.
        var (world, eventBus, combat, validators, _, player, boss) = SetupSwellWorld();
        RegisterTelegraphRungValidator(validators);

        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var connection = new FakeConnection();
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        var clock = new SwellClockManager(world, eventBus, combat, validators, vitalsService: new VitalsService(eventBus));
        var router = new CommandRouter(registry, sessions, world, swellClock: clock);

        var handlerRan = false;
        registry.Register("sidestep", _ => { handlerRan = true; }, pace: Pace.Battle);

        combat.Engage(player, boss);
        clock.AdvanceAll(0);
        clock.AdvanceAll(2); // Telegraph (both lines use "sidestep" in SetupSwellWorld)
        clock.AdvanceAll(4); // Window open

        router.Route(new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "sidestep",
            Command = "sidestep",
            Args = Array.Empty<string>()
        });

        Assert.False(handlerRan); // counter goes through swell clock, not handler

        // Resolve: boss should take chunk damage (COUNTERED)
        clock.AdvanceAll(5);
        Assert.True(boss.Stats.Hp < boss.Stats.MaxHp);
        Assert.Equal(100, player.Stats.Hp);
    }

    [Fact]
    public void Route_Move4_NonCounterBattleVerb_DuringActiveSwell_SendsSlowMessage()
    {
        // Move 4: a non-counter battle verb during an active swell sends "The world has slowed" message.
        var (world, eventBus, combat, validators, _, player, boss) = SetupSwellWorld();
        RegisterTelegraphRungValidator(validators);

        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var connection = new FakeConnection();
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        var clock = new SwellClockManager(world, eventBus, combat, validators, vitalsService: new VitalsService(eventBus));
        var router = new CommandRouter(registry, sessions, world, swellClock: clock);

        var handlerRan = false;
        // "cast" is a non-counter battle verb (not the swell counter)
        registry.Register("cast", _ => { handlerRan = true; }, pace: Pace.Battle);

        combat.Engage(player, boss);
        clock.AdvanceAll(0);
        clock.AdvanceAll(2); // Telegraph (active swell)
        clock.AdvanceAll(4); // Window open

        router.Route(new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "cast",
            Command = "cast",
            Args = Array.Empty<string>()
        });

        Assert.False(handlerRan); // handler blocked during active swell
        // "The world has slowed" message was sent
        Assert.Contains(connection.SentText, t => t.Contains("The world has slowed"));
    }
}
