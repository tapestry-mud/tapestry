using FluentAssertions;
using Jint.Native;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Quests;
using Tapestry.Engine.Tags;
using Tapestry.Engine.Ui;
using Tapestry.Scripting.Modules;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Tests;

public class AdminModuleSetRoomTests
{
    private static AdminModule BuildModule(World world, SessionManager sessions)
    {
        var questMarkerService = new QuestMarkerService(new QuestStateRepository(), new QuestRegistry());
        var messaging = new ApiMessaging(world, sessions, new NullGmcpModuleAdapter(), new CommandResponseContext(), new VisibilityFilter(), questMarkerService);

        var props = new PropertyRegistry();
        props.RegisterEngineProperty("terrain", "Terrain type", PropertyValueType.String,
            appliesTo: new[] { EntityTypes.Room });
        var tags = new TagRegistry();

        return new AdminModule(
            world,
            messaging,
            sessions,
            new PanelRenderer(),
            NullLogger<AdminModule>.Instance,
            props,
            tags,
            new CommandRouter(new CommandRegistry(), sessions, world));
    }

    [Fact]
    public void SetRoom_WritesDeclaredRoomProperty_OnAdminsCurrentRoom()
    {
        var world = new World();
        var sessions = new SessionManager();

        var room = new Room("ns:r1", "Room", "desc");
        world.AddRoom(room);

        var admin = new Entity("player", "Admin");
        room.AddEntity(admin);
        world.TrackEntity(admin);

        var module = BuildModule(world, sessions);
        var engine = new JintEngine();
        dynamic api = module.Build(engine);

        // set room terrain forest  ->  [kind, attr, value]  (rooms have no target token)
        var args = JsValue.FromObject(engine, new[] { "room", "terrain", "forest" });
        ((Action<string, JsValue>)api.set.dispatch)(admin.Id.ToString(), args);

        world.GetRoom("ns:r1")!.GetRawProperty("terrain").Should().Be("forest");
    }
}
