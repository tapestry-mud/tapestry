using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests.Modules;

public class AdminModuleTests
{
    private static readonly string InspectScriptPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "packs", "tapestry-core", "scripts", "commands", "admin-inspect.js"));

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

    private (JintRuntime rt, World world, CommandRegistry commandRegistry, SessionManager sessions) BuildRuntimeWithSessions()
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
            provider.GetRequiredService<SessionManager>()
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
        rt.Execute(script, "tapestry-core", "scripts/commands/admin-inspect.js");
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
        var result = rt.Evaluate("typeof tapestry.admin");
        Assert.Equal("object", result?.ToString());
    }

    [Fact]
    public void SetRegister_StoresRegistration()
    {
        var (rt, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.admin.set.register({
                kind: 'player',
                type: 'testfield',
                applies_to: ['*'],
                help: 'set player testfield <target> <value>',
                handler: function(admin, target, args) {}
            });
        ");
        var result = rt.Evaluate("tapestry.admin.set.listTypes().length");
        Assert.Equal(1, Convert.ToInt32(result));
    }

    [Fact]
    public void GrantRegister_StoresRegistration()
    {
        var (rt, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.admin.grant.register({
                kind: 'player',
                type: 'testgrant',
                applies_to: ['*'],
                help: 'grant player testgrant <target> <amount>',
                handler: function(admin, target, args) {}
            });
        ");
        var result = rt.Evaluate("tapestry.admin.grant.listKinds().length");
        Assert.Equal(1, Convert.ToInt32(result));
    }

    [Fact]
    public void SetRegister_DuplicateKindType_LaterWins()
    {
        var (rt, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.admin.set.register({ kind: 'player', type: 'dup', help: 'first', handler: function(){} });
            tapestry.admin.set.register({ kind: 'player', type: 'dup', help: 'second', handler: function(){} });
        ");
        var result = rt.Evaluate("tapestry.admin.set.getType('player', 'dup').help");
        Assert.Equal("second", result?.ToString());
    }

    [Fact]
    public void ListTypes_ReturnsSortedByType()
    {
        var (rt, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.admin.set.register({ kind: 'player', type: 'zzz', help: 'h', handler: function(){} });
            tapestry.admin.set.register({ kind: 'player', type: 'aaa', help: 'h', handler: function(){} });
        ");
        var first = rt.Evaluate("tapestry.admin.set.listTypes()[0].type");
        Assert.Equal("aaa", first?.ToString());
    }

    [Fact]
    public void ListTypes_IncludesAllFields()
    {
        var (rt, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.admin.set.register({
                kind: 'item',
                type: 'dice',
                applies_to: ['weapon'],
                help: 'set item dice <item> <dice>',
                handler: function(){}
            });
        ");
        var kind = rt.Evaluate("tapestry.admin.set.listTypes()[0].kind");
        var type = rt.Evaluate("tapestry.admin.set.listTypes()[0].type");
        var applies = rt.Evaluate("tapestry.admin.set.listTypes()[0].applies_to[0]");
        Assert.Equal("item", kind?.ToString());
        Assert.Equal("dice", type?.ToString());
        Assert.Equal("weapon", applies?.ToString());
    }

    [Fact]
    public void GetType_ReturnsNullForUnknown()
    {
        var (rt, _) = BuildRuntime();
        var result = rt.Evaluate("tapestry.admin.set.getType('player', 'nope')");
        Assert.True(result == null || result.ToString() == "null" || result.ToString() == "undefined");
    }

    [Fact]
    public void GetType_ReturnsRegistrationForKnown()
    {
        var (rt, _) = BuildRuntime();
        rt.Execute(@"tapestry.admin.set.register({ kind: 'player', type: 'alignment', help: 'h', handler: function(){} });");
        var result = rt.Evaluate("tapestry.admin.set.getType('player', 'alignment').type");
        Assert.Equal("alignment", result?.ToString());
    }

    [Fact]
    public void ResolveTarget_Player_Self_ReturnsSelf()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var ok = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'self', 'player').ok");
        Assert.Equal("true", ok?.ToString()?.ToLower());
    }

    [Fact]
    public void ResolveTarget_Player_NotFound_ReturnsError()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var ok = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'ghostname', 'player').ok");
        var err = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'ghostname', 'player').error");
        Assert.Equal("false", ok?.ToString()?.ToLower());
        Assert.Equal("not_found", err?.ToString());
    }

    [Fact]
    public void ResolveTarget_Npc_NoRoom_ReturnsNoRoomError()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var err = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'elf', 'npc').error");
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

        var name = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'goblin', 'npc').name");
        Assert.Equal("goblin guard", name?.ToString());
    }

    [Fact]
    public void ResolveTarget_Item_NotHeld_ReturnsError()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var err = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'dagger', 'item').error");
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

        var name = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'rusty', 'item').name");
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

        var name = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', '2.goblin', 'npc').name");
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

        var ok = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', '3.goblin', 'npc').ok");
        Assert.Equal("false", ok?.ToString()?.ToLower());
    }

    [Fact]
    public void Dispatch_UnknownKind_DoesNotInvokeHandler()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        rt.Execute(@"
            var _invoked = false;
            tapestry.admin.set.register({ kind: 'player', type: 'alignment', help: 'h', handler: function(){ _invoked = true; } });
        ");
        rt.Execute($"tapestry.admin.set.dispatch('{admin.Id}', ['boguskind'])");
        var invoked = rt.Evaluate("_invoked");
        Assert.Equal("false", invoked?.ToString()?.ToLower());
    }

    [Fact]
    public void Dispatch_UnknownType_DoesNotInvokeHandler()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        rt.Execute(@"
            var _invoked = false;
            tapestry.admin.set.register({ kind: 'player', type: 'alignment', help: 'h', handler: function(){ _invoked = true; } });
        ");
        rt.Execute($"tapestry.admin.set.dispatch('{admin.Id}', ['player', 'nope', 'mallek', '100'])");
        var invoked = rt.Evaluate("_invoked");
        Assert.Equal("false", invoked?.ToString()?.ToLower());
    }

    [Fact]
    public void Dispatch_HappyPath_InvokesHandlerWithCorrectArgs()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        rt.Execute(@"
            var _result = null;
            tapestry.admin.set.register({
                kind: 'player',
                type: 'testfield',
                help: 'set player testfield <target> <value>',
                handler: function(admin, target, args) {
                    _result = { targetId: target.id, arg0: args[0] };
                }
            });
        ");
        rt.Execute($"tapestry.admin.set.dispatch('{admin.Id}', ['player', 'testfield', 'self', '999'])");
        var targetId = rt.Evaluate("_result.targetId");
        var arg0 = rt.Evaluate("_result.arg0");
        Assert.Equal(admin.Id.ToString(), targetId?.ToString());
        Assert.Equal("999", arg0?.ToString());
    }

    [Fact]
    public void Dispatch_SubtypeMismatch_DoesNotInvokeHandler()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var armor = new Entity("item:armor", "leather helm");
        world.TrackEntity(armor);
        admin.AddToContents(armor);
        rt.Execute(@"
            var _invoked = false;
            tapestry.admin.set.register({
                kind: 'item',
                type: 'dice',
                applies_to: ['weapon'],
                help: 'set item dice <item> <dice>',
                handler: function(){ _invoked = true; }
            });
        ");
        rt.Execute($"tapestry.admin.set.dispatch('{admin.Id}', ['item', 'dice', 'leather', '2d6'])");
        var invoked = rt.Evaluate("_invoked");
        Assert.Equal("false", invoked?.ToString()?.ToLower());
    }

    [Fact]
    public void Dispatch_AppliesToStar_InvokesHandlerRegardlessOfSubtype()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var item = new Entity("item:armor", "leather helm");
        world.TrackEntity(item);
        admin.AddToContents(item);
        rt.Execute(@"
            var _invoked = false;
            tapestry.admin.set.register({
                kind: 'item',
                type: 'name',
                applies_to: ['*'],
                help: 'set item name <item> <value>',
                handler: function(){ _invoked = true; }
            });
        ");
        rt.Execute($"tapestry.admin.set.dispatch('{admin.Id}', ['item', 'name', 'leather', 'shiny helm'])");
        var invoked = rt.Evaluate("_invoked");
        Assert.Equal("true", invoked?.ToString()?.ToLower());
    }

    [Fact]
    public void SetRegister_ReturnsFullShapeFromListTypes()
    {
        var (rt, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.admin.set.register({
                kind: 'item',
                type: 'dice',
                applies_to: ['weapon'],
                help: 'set item dice <item> <dice>',
                handler: function(admin, target, args) {}
            });
        ");
        var kind = rt.Evaluate("tapestry.admin.set.listTypes()[0].kind");
        var type = rt.Evaluate("tapestry.admin.set.listTypes()[0].type");
        var appliesTo = rt.Evaluate("tapestry.admin.set.listTypes()[0].applies_to[0]");
        var help = rt.Evaluate("tapestry.admin.set.listTypes()[0].help");
        Assert.Equal("item", kind?.ToString());
        Assert.Equal("dice", type?.ToString());
        Assert.Equal("weapon", appliesTo?.ToString());
        Assert.Equal("set item dice <item> <dice>", help?.ToString());
    }

    [Fact]
    public void GrantRegister_ReturnsFullShapeFromListKinds()
    {
        var (rt, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.admin.grant.register({
                kind: 'player',
                type: 'xp',
                applies_to: ['*'],
                help: 'grant player xp <target> <amount> [track]',
                handler: function(admin, target, args) {}
            });
        ");
        var kind = rt.Evaluate("tapestry.admin.grant.listKinds()[0].kind");
        var type = rt.Evaluate("tapestry.admin.grant.listKinds()[0].type");
        Assert.Equal("player", kind?.ToString());
        Assert.Equal("xp", type?.ToString());
    }

    [Fact]
    public void ResolveTarget_ReturnsExpectedShapeOnSuccess()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var ok = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'self', 'player').ok");
        var name = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'self', 'player').name");
        Assert.Equal("true", ok?.ToString()?.ToLower());
        Assert.Equal("AdminTester", name?.ToString());
    }

    [Fact]
    public void ResolveTarget_ReturnsExpectedShapeOnFailure()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        var ok = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'nobody', 'player').ok");
        var error = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'nobody', 'player').error");
        var message = rt.Evaluate($"tapestry.admin.resolveTarget('{admin.Id}', 'nobody', 'player').message");
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
        mob.Stats.Hp = 50;

        rt.Execute($"tapestry.admin.setEntityHp('{mob.Id}', 8000)");

        Assert.Equal(8000, mob.Stats.BaseMaxHp);
        Assert.Equal(8000, mob.Stats.MaxHp);
        Assert.Equal(8000, mob.Stats.Hp);
    }

    [Fact]
    public void SetEntityHp_InvalidId_DoesNotThrow()
    {
        var (rt, _) = BuildRuntime();
        var ex = Record.Exception(() => rt.Execute("tapestry.admin.setEntityHp('not-a-guid', 100)"));
        Assert.Null(ex);
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

        var ctx = new CommandContext
        {
            PlayerEntityId = admin.Id,
            RawInput = "inspect goblin",
            Command = "inspect",
            Args = new[] { "goblin" }
        };
        registry.Resolve("inspect")!.Handler(ctx);

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

        var ctx = new CommandContext
        {
            PlayerEntityId = admin.Id,
            RawInput = "inspect 1.goblin",
            Command = "inspect",
            Args = new[] { "1.goblin" }
        };
        registry.Resolve("inspect")!.Handler(ctx);

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

        var ctx = new CommandContext
        {
            PlayerEntityId = admin.Id,
            RawInput = "inspect 2.goblin",
            Command = "inspect",
            Args = new[] { "2.goblin" }
        };
        registry.Resolve("inspect")!.Handler(ctx);

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

        var ctx = new CommandContext
        {
            PlayerEntityId = admin.Id,
            RawInput = "inspect 3.goblin",
            Command = "inspect",
            Args = new[] { "3.goblin" }
        };
        registry.Resolve("inspect")!.Handler(ctx);

        var output = string.Join("", conn.SentText);
        Assert.Contains("Nothing named", output);
    }

    [Fact]
    public void Dispatch_HelpPath_DoesNotInvokeHandler()
    {
        var (rt, world) = BuildRuntime();
        var admin = CreateAdmin(world);
        rt.Execute(@"
            var _invoked = false;
            tapestry.admin.set.register({ kind: 'player', type: 'alignment', help: 'h', handler: function(){ _invoked = true; } });
        ");
        rt.Execute($"tapestry.admin.set.dispatch('{admin.Id}', [])");
        var invoked = rt.Evaluate("_invoked");
        Assert.Equal("false", invoked?.ToString()?.ToLower());
        rt.Execute($"tapestry.admin.set.dispatch('{admin.Id}', ['?'])");
        Assert.Equal("false", rt.Evaluate("_invoked")?.ToString()?.ToLower());
        rt.Execute($"tapestry.admin.set.dispatch('{admin.Id}', ['player', '?'])");
        Assert.Equal("false", rt.Evaluate("_invoked")?.ToString()?.ToLower());
        rt.Execute($"tapestry.admin.set.dispatch('{admin.Id}', ['player', 'alignment', '?'])");
        Assert.Equal("false", rt.Evaluate("_invoked")?.ToString()?.ToLower());
    }

    [Fact]
    public void LinkCommand_NonAdminPlayer_ReceivesHuhAndFlowNotTriggered()
    {
        var (rt, world, commandRegistry, sessions) = BuildRuntimeWithSessions();
        var connection = new FakeConnection();
        var player = new Entity("player", "NonAdmin");
        world.TrackEntity(player);
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        rt.Execute(@"
            tapestry.commands.register({
                name: 'link',
                aliases: [],
                description: 'Link rooms across packs via guided flow.',
                priority: 10,
                handler: function(player, args) {
                    if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
                    player.send(""Starting link wizard. Type 'cancel' or 'quit' to exit at any time.\r\n"");
                    tapestry.flows.trigger(player.entityId, 'admin_link');
                }
            });
        ");

        var registration = commandRegistry.Resolve("link");
        Assert.NotNull(registration);

        var cmdCtx = new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "link",
            Command = "link",
            Args = []
        };
        registration!.Handler(cmdCtx);

        Assert.Contains("Huh?", string.Join("", connection.SentText));
        Assert.DoesNotContain("Starting link wizard", string.Join("", connection.SentText));
    }

    [Fact]
    public void UnlinkCommand_NonAdminPlayer_ReceivesHuhAndFlowNotTriggered()
    {
        var (rt, world, commandRegistry, sessions) = BuildRuntimeWithSessions();
        var connection = new FakeConnection();
        var player = new Entity("player", "NonAdmin");
        world.TrackEntity(player);
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        rt.Execute(@"
            tapestry.commands.register({
                name: 'unlink',
                aliases: [],
                description: 'Remove a connection from this room.',
                priority: 10,
                handler: function(player, args) {
                    if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
                    player.send(""Starting unlink wizard. Type 'cancel' or 'quit' to exit at any time.\r\n"");
                    tapestry.flows.trigger(player.entityId, 'admin_unlink');
                }
            });
        ");

        var registration = commandRegistry.Resolve("unlink");
        Assert.NotNull(registration);

        var cmdCtx = new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "unlink",
            Command = "unlink",
            Args = []
        };
        registration!.Handler(cmdCtx);

        Assert.Contains("Huh?", string.Join("", connection.SentText));
        Assert.DoesNotContain("Starting unlink wizard", string.Join("", connection.SentText));
    }

    [Fact]
    public void ConnectionsCommand_NonAdminPlayer_ReceivesHuhAndListingNotShown()
    {
        var (rt, world, commandRegistry, sessions) = BuildRuntimeWithSessions();
        var connection = new FakeConnection();
        var player = new Entity("player", "NonAdmin");
        world.TrackEntity(player);
        var session = new PlayerSession(connection, player);
        sessions.Add(session);

        rt.Execute(@"
            tapestry.commands.register({
                name: 'connections',
                aliases: [],
                description: 'List connections for this room or all rooms.',
                priority: 10,
                handler: function(player, args) {
                    if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
                    var conns = tapestry.connections.getForRoom(player.roomId);
                    if (conns.length === 0) {
                        player.send('No connections for this room.\r\n');
                        return;
                    }
                    player.send('Connections for ' + player.roomId + ':\r\n');
                }
            });
        ");

        var registration = commandRegistry.Resolve("connections");
        Assert.NotNull(registration);

        var cmdCtx = new CommandContext
        {
            PlayerEntityId = player.Id,
            RawInput = "connections",
            Command = "connections",
            Args = []
        };
        registration!.Handler(cmdCtx);

        Assert.Contains("Huh?", string.Join("", connection.SentText));
        Assert.DoesNotContain("Connections for", string.Join("", connection.SentText));
        Assert.DoesNotContain("No connections", string.Join("", connection.SentText));
    }
}
