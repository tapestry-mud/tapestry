using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Scripting;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests.Modules;

public class AlignmentRoomGateTests
{
    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void MoveEntity_BlockedByAlignmentRange_ReturnsFalse()
    {
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var rt = sp.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var alignmentMgr = sp.GetRequiredService<AlignmentManager>();
        var published = new List<GameEvent>();
        sp.GetRequiredService<EventBus>().Subscribe("room.enter.blocked", e => published.Add(e));

        // Create two rooms with a north exit; target room requires alignment <= -500
        var from = new Room("test:from", "Start", "Starting room.");
        from.SetExit(Direction.North, new Exit("test:pit"));
        var to = new Room("test:pit", "The Pit", "Dark.");
        to.AlignmentRange = new AlignmentRange { Max = -500 };
        to.AlignmentBlockMessage = "Rejected.\r\n";
        world.AddRoom(from);
        world.AddRoom(to);

        var player = new Entity("player", "Tester");
        player.AddTag("player");
        world.TrackEntity(player);
        from.AddEntity(player);
        alignmentMgr.Set(player.Id, 0, "init");  // neutral — fails gate

        var result = rt.Evaluate($"tapestry.world.moveEntity('{player.Id}', 'north')");
        Assert.False((bool)result!);
        Assert.Equal("test:from", player.LocationRoomId);
        Assert.Single(published);
    }

    [Fact]
    public void MoveEntity_AdminBypassesAlignmentGate()
    {
        var sp = BuildProvider();
        var world = sp.GetRequiredService<World>();
        var rt = sp.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var alignmentMgr = sp.GetRequiredService<AlignmentManager>();

        var from = new Room("test2:from", "Start", "Start.");
        from.SetExit(Direction.North, new Exit("test2:pit"));
        var to = new Room("test2:pit", "Pit", "Dark.");
        to.AlignmentRange = new AlignmentRange { Max = -500 };
        world.AddRoom(from);
        world.AddRoom(to);

        var admin = new Entity("player", "Admin");
        admin.AddTag("player");
        admin.AddTag("admin");
        world.TrackEntity(admin);
        from.AddEntity(admin);
        alignmentMgr.Set(admin.Id, 0, "init");

        var result = rt.Evaluate($"tapestry.world.moveEntity('{admin.Id}', 'north')");
        Assert.True((bool)result!);
        Assert.Equal("test2:pit", admin.LocationRoomId);
    }
}
