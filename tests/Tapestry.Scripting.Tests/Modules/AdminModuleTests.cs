using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Stats;
using Tapestry.Scripting;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests.Modules;

public class AdminModuleTests
{
    private static readonly string InspectScriptPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "fixtures", "packs", "tapestry-core", "scripts", "commands", "admin-inspect.js"));

    private (JintRuntime rt, World world) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>());
    }

    private (JintRuntime rt, World world, CommandRegistry commandRegistry, SessionManager sessions, Tapestry.Engine.Registration.RegistrationPolicy policy) BuildRuntimeWithSessions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (
            rt,
            provider.GetRequiredService<World>(),
            provider.GetRequiredService<CommandRegistry>(),
            provider.GetRequiredService<SessionManager>(),
            provider.GetRequiredService<Tapestry.Engine.Registration.RegistrationPolicy>()
        );
    }

    private (JintRuntime rt, World world, CommandRegistry registry, SessionManager sessions) BuildInspectRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var world = provider.GetRequiredService<World>();
        var registry = provider.GetRequiredService<CommandRegistry>();
        var sessions = provider.GetRequiredService<SessionManager>();
        var script = File.ReadAllText(InspectScriptPath);
        EsmTest.Load(rt, "tapestry-core", script, "scripts/commands/admin-inspect.js");
        // Command registration is now ledgered; seal so the registry is populated before dispatch.
        provider.GetRequiredService<Tapestry.Engine.Registration.RegistrationPolicy>().Resolve();
        return (rt, world, registry, sessions);
    }

    private (Entity admin, FakeConnection conn) CreateAdminSession(World world, SessionManager sessions, Room room)
    {
        var conn = new FakeConnection();
        var admin = new Entity("player", "AdminTester");
        admin.AddTag("admin");
        admin.AddTag("player");
        world.TrackEntity(admin);
        room.AddEntity(admin);
        sessions.Add(new PlayerSession(conn, admin));
        return (admin, conn);
    }

    private Entity CreateNpc(World world, Room room, string name)
    {
        var mob = new Entity("npc", name);
        mob.AddTag("npc");
        world.TrackEntity(mob);
        room.AddEntity(mob);
        return mob;
    }

    private Entity CreateAdmin(World world)
    {
        var e = new Entity("player", "AdminTester");
        e.AddTag("admin");
        world.TrackEntity(e);
        return e;
    }

    [Fact]
    public void AdminNamespace_IsAccessibleAfterInitialize()
    {
        var (rt, _) = BuildRuntime();
        var result = EsmTest.Eval(rt, "typeof tapestry.admin");
        Assert.Equal("object", result?.ToString());
    }

    [Fact]
    public void GrantRegister_StoresRegistration()
    {
        var (rt, _) = BuildRuntime();
        EsmTest.Load(rt, "test-pack", @"
            tapestry.admin.grant.register({
                kind: 'player',
                type: 'testgrant',
                applies_to: ['*'],
                help: 'grant player testgrant <target> <amount>',
                handler: function(admin, target, args) {}
            });
        ");
        var result = EsmTest.Eval(rt, "tapestry.admin.grant.listKinds().length");
        Assert.Equal(1, Convert.ToInt32(result));
    }

    [Fact]
    public void ResolveTarget_Player_Self_ReturnsSelf()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var ok = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'self', 'player').ok");
        Assert.Equal("true", ok?.ToString()?.ToLower());
    }

    [Fact]
    public void ResolveTarget_Player_NotFound_ReturnsError()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var ok = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'ghostname', 'player').ok");
        var err = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'ghostname', 'player').error");
        Assert.Equal("false", ok?.ToString()?.ToLower());
        Assert.Equal("not_found", err?.ToString());
    }

    [Fact]
    public void ResolveTarget_Npc_NoRoom_ReturnsNoRoomError()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var err = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'elf', 'npc').error");
        Assert.Equal("no_room", err?.ToString());
    }

    [Fact]
    public void ResolveTarget_Npc_InRoom_ReturnsMatch()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var room = new Room("test:spawn", "Test Room", "A test room.");
        world.AddRoom(room);
        admin.LocationRoomId = room.Id;
        room.AddEntity(admin);

        var mob = new Entity("npc", "goblin guard");
        mob.AddTag("npc");
        world.TrackEntity(mob);
        room.AddEntity(mob);

        var name = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'goblin', 'npc').name");
        Assert.Equal("goblin guard", name?.ToString());
    }

    [Fact]
    public void ResolveTarget_Item_NotHeld_ReturnsError()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var err = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'dagger', 'item').error");
        Assert.Equal("not_held", err?.ToString());
    }

    [Fact]
    public void ResolveTarget_Item_InInventory_ReturnsMatch()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var item = new Entity("item:weapon", "rusty dagger");
        item.AddTag("dagger");
        world.TrackEntity(item);
        admin.AddToContents(item);

        var name = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'rusty', 'item').name");
        Assert.Equal("rusty dagger", name?.ToString());
    }

    [Fact]
    public void ResolveTarget_Ordinal_SecondMatch()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var room = new Room("test:ordinal", "Ordinal Room", "A test room.");
        world.AddRoom(room);
        admin.LocationRoomId = room.Id;
        room.AddEntity(admin);

        var mob1 = new Entity("npc", "goblin guard");
        mob1.AddTag("npc");
        world.TrackEntity(mob1);
        room.AddEntity(mob1);

        var mob2 = new Entity("npc", "goblin warrior");
        mob2.AddTag("npc");
        world.TrackEntity(mob2);
        room.AddEntity(mob2);

        var name = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', '2.goblin', 'npc').name");
        Assert.Equal("goblin warrior", name?.ToString());
    }

    [Fact]
    public void ResolveTarget_OrdinalOutOfRange_ReturnsError()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var room = new Room("test:ordinal2", "Ordinal Room 2", "A test room.");
        world.AddRoom(room);
        admin.LocationRoomId = room.Id;
        room.AddEntity(admin);

        var mob = new Entity("npc", "goblin guard");
        mob.AddTag("npc");
        world.TrackEntity(mob);
        room.AddEntity(mob);

        var ok = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', '3.goblin', 'npc').ok");
        Assert.Equal("false", ok?.ToString()?.ToLower());
    }

    [Fact]
    public void GrantRegister_ReturnsFullShapeFromListKinds()
    {
        var (rt, _) = BuildRuntime();
        EsmTest.Load(rt, "test-pack", @"
            tapestry.admin.grant.register({
                kind: 'player',
                type: 'xp',
                applies_to: ['*'],
                help: 'grant player xp <target> <amount> [track]',
                handler: function(admin, target, args) {}
            });
        ");
        var kind = EsmTest.Eval(rt, "tapestry.admin.grant.listKinds()[0].kind");
        var type = EsmTest.Eval(rt, "tapestry.admin.grant.listKinds()[0].type");
        Assert.Equal("player", kind?.ToString());
        Assert.Equal("xp", type?.ToString());
    }

    [Fact]
    public void ResolveTarget_ReturnsExpectedShapeOnSuccess()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var ok = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'self', 'player').ok");
        var name = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'self', 'player').name");
        Assert.Equal("true", ok?.ToString()?.ToLower());
        Assert.Equal("AdminTester", name?.ToString());
    }

    [Fact]
    public void ResolveTarget_ReturnsExpectedShapeOnFailure()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var ok = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'nobody', 'player').ok");
        var error = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'nobody', 'player').error");
        var message = EsmTest.Eval(rt, $"tapestry.admin.resolveTarget('{admin.Id}', 'nobody', 'player').message");
        Assert.Equal("false", ok?.ToString()?.ToLower());
        Assert.Equal("not_found", error?.ToString());
        Assert.Contains("nobody", message?.ToString() ?? "");
    }

    [Fact]
    public void SetEntityHp_SetsBaseMaxHpAndClampsCurrentHp()
    {
        var (rt, world) = BuildRuntime();
        var mob = new Entity("npc", "goblin guard");
        mob.AddTag("npc");
        world.TrackEntity(mob);
        mob.Stats.BaseMaxHp = 100;
        mob.Stats.Invalidate();
        mob.Stats.SetVital(VitalKind.Hp, 50);

        EsmTest.Load(rt, "test-pack", $"tapestry.admin.setEntityHp('{mob.Id}', 8000)");

        Assert.Equal(8000, mob.Stats.BaseMaxHp);
        Assert.Equal(8000, mob.Stats.MaxHp);
        Assert.Equal(8000, mob.Stats.Hp);
    }

    [Fact]
    public void SetEntityHp_InvalidId_DoesNotThrow()
    {
        var (rt, _) = BuildRuntime();
        var ex = Record.Exception(() => EsmTest.Load(rt, "test-pack", "tapestry.admin.setEntityHp('not-a-guid', 100)"));
        Assert.Null(ex);
    }

    [Fact]
    public void SetEntityHp_PublishesVitalChanged_WithAdminReason()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var world = provider.GetRequiredService<World>();
        var eventBus = provider.GetRequiredService<EventBus>();

        var mob = new Entity("npc", "goblin guard");
        mob.AddTag("npc");
        world.TrackEntity(mob);
        mob.Stats.BaseMaxHp = 100;
        mob.Stats.Invalidate();
        mob.Stats.SetVital(VitalKind.Hp, 50);

        var seen = new List<GameEvent>();
        eventBus.Subscribe("entity.vital.changed", seen.Add);

        EsmTest.Load(rt, "test-pack", $"tapestry.admin.setEntityHp('{mob.Id}', 8000)");

        Assert.Equal(8000, mob.Stats.Hp);
        var evt = Assert.Single(seen);
        Assert.Equal("hp", evt.Data["vital"]);
        Assert.Equal("admin", evt.Data["reason"]);
    }

    [Fact]
    public void InspectCommand_NoOrdinal_FindsFirstGoblin()
    {
        var (rt, world, registry, sessions) = BuildInspectRuntime();
        var room = new Room("test:inspect1", "Test Room", "A room.");
        world.AddRoom(room);
        var (admin, conn) = CreateAdminSession(world, sessions, room);
        CreateNpc(world, room, "goblin guard");
        CreateNpc(world, room, "goblin warrior");

        var ctx = new ActorContext
        {
            EntityId = admin.Id,
            RoomId = room.Id,
            RawInput = "inspect goblin",
            Command = "inspect",
            RawArgs = new[] { "goblin" }
        };
        registry.Resolve("inspect")!.ActorHandler(ctx);

        var output = string.Join("", conn.SentText);
        Assert.Contains("goblin guard", output);
    }

    [Fact]
    public void InspectCommand_Ordinal1_FindsFirstGoblin()
    {
        var (rt, world, registry, sessions) = BuildInspectRuntime();
        var room = new Room("test:inspect2", "Test Room", "A room.");
        world.AddRoom(room);
        var (admin, conn) = CreateAdminSession(world, sessions, room);
        CreateNpc(world, room, "goblin guard");
        CreateNpc(world, room, "goblin warrior");

        var ctx = new ActorContext
        {
            EntityId = admin.Id,
            RoomId = room.Id,
            RawInput = "inspect 1.goblin",
            Command = "inspect",
            RawArgs = new[] { "1.goblin" }
        };
        registry.Resolve("inspect")!.ActorHandler(ctx);

        var output = string.Join("", conn.SentText);
        Assert.Contains("goblin guard", output);
    }

    [Fact]
    public void InspectCommand_Ordinal2_FindsSecondGoblin()
    {
        var (rt, world, registry, sessions) = BuildInspectRuntime();
        var room = new Room("test:inspect3", "Test Room", "A room.");
        world.AddRoom(room);
        var (admin, conn) = CreateAdminSession(world, sessions, room);
        CreateNpc(world, room, "goblin guard");
        CreateNpc(world, room, "goblin warrior");

        var ctx = new ActorContext
        {
            EntityId = admin.Id,
            RoomId = room.Id,
            RawInput = "inspect 2.goblin",
            Command = "inspect",
            RawArgs = new[] { "2.goblin" }
        };
        registry.Resolve("inspect")!.ActorHandler(ctx);

        var output = string.Join("", conn.SentText);
        Assert.Contains("goblin warrior", output);
        Assert.DoesNotContain("goblin guard", output);
    }

    [Fact]
    public void InspectCommand_OrdinalOutOfRange_ReturnsNothingNamed()
    {
        var (rt, world, registry, sessions) = BuildInspectRuntime();
        var room = new Room("test:inspect4", "Test Room", "A room.");
        world.AddRoom(room);
        var (admin, conn) = CreateAdminSession(world, sessions, room);
        CreateNpc(world, room, "goblin guard");
        CreateNpc(world, room, "goblin warrior");

        var ctx = new ActorContext
        {
            EntityId = admin.Id,
            RoomId = room.Id,
            RawInput = "inspect 3.goblin",
            Command = "inspect",
            RawArgs = new[] { "3.goblin" }
        };
        registry.Resolve("inspect")!.ActorHandler(ctx);

        var output = string.Join("", conn.SentText);
        Assert.Contains("You don't see that here", output);
    }

    [Fact]
    public void LinkCommand_NonAdminPlayer_ReceivesHuhAndFlowNotTriggered()
    {
        var (rt, world, commandRegistry, sessions, policy) = BuildRuntimeWithSessions();
        var connection = new FakeConnection();
        var player = new Entity("player", "NonAdmin");
        world.TrackEntity(player);
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        EsmTest.Load(rt, "test-pack", @"
            tapestry.commands.register({
                name: 'link',
                aliases: [],
                description: 'Link rooms across packs via guided flow.',
                priority: 10,
                handler: function(player, args) {
                    if (!player.hasRole('admin')) { player.send('Huh?\r\n'); return; }
                    player.send(""Starting link wizard. Type 'cancel' or 'quit' to exit at any time.\r\n"");
                    tapestry.flows.trigger(player.entityId, 'admin_link');
                }
            });
        ");
        policy.Resolve();

        var registration = commandRegistry.Resolve("link");
        Assert.NotNull(registration);

        var cmdCtx = new ActorContext
        {
            EntityId = player.Id,
            RawInput = "link",
            Command = "link",
            RawArgs = []
        };
        registration!.ActorHandler(cmdCtx);

        Assert.Contains("Huh?", string.Join("", connection.SentText));
        Assert.DoesNotContain("Starting link wizard", string.Join("", connection.SentText));
    }

    [Fact]
    public void UnlinkCommand_NonAdminPlayer_ReceivesHuhAndFlowNotTriggered()
    {
        var (rt, world, commandRegistry, sessions, policy) = BuildRuntimeWithSessions();
        var connection = new FakeConnection();
        var player = new Entity("player", "NonAdmin");
        world.TrackEntity(player);
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        EsmTest.Load(rt, "test-pack", @"
            tapestry.commands.register({
                name: 'unlink',
                aliases: [],
                description: 'Remove a connection from this room.',
                priority: 10,
                handler: function(player, args) {
                    if (!player.hasRole('admin')) { player.send('Huh?\r\n'); return; }
                    player.send(""Starting unlink wizard. Type 'cancel' or 'quit' to exit at any time.\r\n"");
                    tapestry.flows.trigger(player.entityId, 'admin_unlink');
                }
            });
        ");
        policy.Resolve();

        var registration = commandRegistry.Resolve("unlink");
        Assert.NotNull(registration);

        var cmdCtx = new ActorContext
        {
            EntityId = player.Id,
            RawInput = "unlink",
            Command = "unlink",
            RawArgs = []
        };
        registration!.ActorHandler(cmdCtx);

        Assert.Contains("Huh?", string.Join("", connection.SentText));
        Assert.DoesNotContain("Starting unlink wizard", string.Join("", connection.SentText));
    }

    // --- executeAs: synchronous dispatch-as-entity (force/at seam) ---

    private (Entity target, FakeConnection conn) CreatePlayingSession(
        World world, SessionManager sessions, string name = "Target")
    {
        var conn = new FakeConnection();
        var target = new Entity("player", name);
        world.TrackEntity(target);
        var session = new PlayerSession(conn, target) { Phase = LoginPhase.Playing };
        sessions.Add(session);
        return (target, conn);
    }

    [Fact]
    public void ExecuteAs_DispatchesCommandAsTargetEntity_ReturnsTrue()
    {
        var (rt, world, _, sessions, policy) = BuildRuntimeWithSessions();
        var (target, _) = CreatePlayingSession(world, sessions);

        EsmTest.Load(rt, "test-pack", @"
            globalThis.waveActor = null;
            tapestry.commands.register({
                name: 'wave',
                description: 'Wave.',
                priority: 10,
                handler: function(player, args) { globalThis.waveActor = player.entityId; }
            });
        ");
        policy.Resolve();

        var result = EsmTest.Eval(rt, $"tapestry.admin.executeAs('{target.Id}', 'wave')");

        Assert.Equal("true", result?.ToString()?.ToLower());
        Assert.Equal(target.Id.ToString(), rt.Evaluate("globalThis.waveActor")?.ToString());
    }

    [Fact]
    public void ExecuteAs_ArgsFlowThroughRealParse()
    {
        var (rt, world, _, sessions, policy) = BuildRuntimeWithSessions();
        var (target, _) = CreatePlayingSession(world, sessions);

        EsmTest.Load(rt, "test-pack", @"
            globalThis.sayArgs = null;
            tapestry.commands.register({
                name: 'say',
                description: 'Say.',
                priority: 10,
                handler: function(player, args) { globalThis.sayArgs = args; }
            });
        ");
        policy.Resolve();

        var result = EsmTest.Eval(rt, $"tapestry.admin.executeAs('{target.Id}', 'say hello world')");

        Assert.Equal("true", result?.ToString()?.ToLower());
        Assert.Equal(2, Convert.ToInt32(rt.Evaluate("globalThis.sayArgs.length")));
        Assert.Equal("hello", rt.Evaluate("globalThis.sayArgs[0]")?.ToString());
        Assert.Equal("world", rt.Evaluate("globalThis.sayArgs[1]")?.ToString());
    }

    [Fact]
    public void ExecuteAs_UnknownEntity_ReturnsFalse_NothingDispatched()
    {
        var (rt, world, _, sessions, policy) = BuildRuntimeWithSessions();
        CreatePlayingSession(world, sessions);

        EsmTest.Load(rt, "test-pack", @"
            globalThis.waveActor = null;
            tapestry.commands.register({
                name: 'wave',
                description: 'Wave.',
                priority: 10,
                handler: function(player, args) { globalThis.waveActor = player.entityId; }
            });
        ");
        policy.Resolve();

        var result = EsmTest.Eval(rt, $"tapestry.admin.executeAs('{Guid.NewGuid()}', 'wave')");

        Assert.Equal("false", result?.ToString()?.ToLower());
        Assert.Equal("null", rt.Evaluate("globalThis.waveActor === null ? 'null' : 'set'")?.ToString());
    }

    [Fact]
    public void ExecuteAs_GarbageGuid_ReturnsFalse()
    {
        var (rt, _, _, _, _) = BuildRuntimeWithSessions();
        var result = EsmTest.Eval(rt, "tapestry.admin.executeAs('not-a-guid', 'wave')");
        Assert.Equal("false", result?.ToString()?.ToLower());
    }

    [Fact]
    public void ExecuteAs_BlankCommand_ReturnsFalse()
    {
        var (rt, world, _, sessions, _) = BuildRuntimeWithSessions();
        var (target, _) = CreatePlayingSession(world, sessions);

        var blank = EsmTest.Eval(rt, $"tapestry.admin.executeAs('{target.Id}', '')");
        var whitespace = EsmTest.Eval(rt, $"tapestry.admin.executeAs('{target.Id}', '   ')");

        Assert.Equal("false", blank?.ToString()?.ToLower());
        Assert.Equal("false", whitespace?.ToString()?.ToLower());
    }

    [Fact]
    public void ExecuteAs_DoesNotEscalatePrivilege_TargetGetsHuh()
    {
        var (rt, world, _, sessions, policy) = BuildRuntimeWithSessions();
        var (target, conn) = CreatePlayingSession(world, sessions);

        EsmTest.Load(rt, "test-pack", @"
            globalThis.secretRan = false;
            tapestry.commands.register({
                name: 'secret',
                description: 'Admin only.',
                priority: 10,
                roles: ['admin'],
                handler: function(player, args) { globalThis.secretRan = true; }
            });
        ");
        policy.Resolve();

        // Target has NO admin role: the router must re-gate as the TARGET and deny.
        var result = EsmTest.Eval(rt, $"tapestry.admin.executeAs('{target.Id}', 'secret')");

        Assert.Equal("true", result?.ToString()?.ToLower()); // dispatched into the router...
        Assert.Equal("false", rt.Evaluate("globalThis.secretRan")?.ToString()?.ToLower()); // ...but denied
        Assert.Contains("Huh?", string.Join("", conn.SentText));
    }

    [Fact]
    public void ExecuteAs_ForcedOutputGoesToTargetSession()
    {
        var (rt, world, _, sessions, policy) = BuildRuntimeWithSessions();
        var (target, targetConn) = CreatePlayingSession(world, sessions, "Target");
        var (_, forcerConn) = CreatePlayingSession(world, sessions, "Forcer");

        EsmTest.Load(rt, "test-pack", @"
            tapestry.commands.register({
                name: 'greet',
                description: 'Greet.',
                priority: 10,
                handler: function(player, args) { player.send('forced hello\r\n'); }
            });
        ");
        policy.Resolve();

        var result = EsmTest.Eval(rt, $"tapestry.admin.executeAs('{target.Id}', 'greet')");

        Assert.Equal("true", result?.ToString()?.ToLower());
        Assert.Contains("forced hello", string.Join("", targetConn.SentText));
        Assert.DoesNotContain("forced hello", string.Join("", forcerConn.SentText));
    }

    [Fact]
    public void ConnectionsCommand_NonAdminPlayer_ReceivesHuhAndListingNotShown()
    {
        var (rt, world, commandRegistry, sessions, policy) = BuildRuntimeWithSessions();
        var connection = new FakeConnection();
        var player = new Entity("player", "NonAdmin");
        world.TrackEntity(player);
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        EsmTest.Load(rt, "test-pack", @"
            tapestry.commands.register({
                name: 'connections',
                aliases: [],
                description: 'List connections for this room or all rooms.',
                priority: 10,
                handler: function(player, args) {
                    if (!player.hasRole('admin')) { player.send('Huh?\r\n'); return; }
                    var conns = tapestry.connections.getForRoom(player.roomId);
                    if (conns.length === 0) {
                        player.send('No connections for this room.\r\n');
                        return;
                    }
                    player.send('Connections for ' + player.roomId + ':\r\n');
                }
            });
        ");
        policy.Resolve();

        var registration = commandRegistry.Resolve("connections");
        Assert.NotNull(registration);

        var cmdCtx = new ActorContext
        {
            EntityId = player.Id,
            RawInput = "connections",
            Command = "connections",
            RawArgs = []
        };
        registration!.ActorHandler(cmdCtx);

        Assert.Contains("Huh?", string.Join("", connection.SentText));
        Assert.DoesNotContain("Connections for", string.Join("", connection.SentText));
        Assert.DoesNotContain("No connections", string.Join("", connection.SentText));
    }

    [Fact]
    public void Wizlock_DefaultsUnlocked_AndTogglesViaJs()
    {
        var (rt, _) = BuildRuntime();

        Assert.False(Convert.ToBoolean(EsmTest.Eval(rt, "tapestry.admin.isWizlocked()")));

        EsmTest.Load(rt, "test-pack", "tapestry.admin.setWizlock(true);");
        Assert.True(Convert.ToBoolean(EsmTest.Eval(rt, "tapestry.admin.isWizlocked()")));

        EsmTest.Load(rt, "test-pack", "tapestry.admin.setWizlock(false);");
        Assert.False(Convert.ToBoolean(EsmTest.Eval(rt, "tapestry.admin.isWizlocked()")));
    }
}
