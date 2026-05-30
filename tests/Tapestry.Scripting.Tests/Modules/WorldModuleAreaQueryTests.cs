using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

/// <summary>
/// Verifies the room-area query helpers on the JS world surface that the
/// @tapestry/builder pack relies on to mint unique room keys and read a
/// room's area. Drives through the real JintRuntime against a real World.
/// </summary>
public class WorldModuleAreaQueryTests
{
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

    [Fact]
    public void GetRoomsInArea_ReturnsRoomIdsWhoseAreaMatches()
    {
        var (rt, world) = BuildRuntime();
        var a1 = new Room("lf:1", "Room A1", "first") { Area = "lf-test" };
        var a2 = new Room("lf:2", "Room A2", "second") { Area = "lf-test" };
        var other = new Room("other:1", "Other", "third") { Area = "other-area" };
        world.AddRoom(a1);
        world.AddRoom(a2);
        world.AddRoom(other);

        var result = rt.Evaluate("tapestry.world.getRoomsInArea('lf-test').sort().join(',');");

        Assert.Equal("lf:1,lf:2", result);
    }

    [Fact]
    public void GetRoomsInArea_UnknownArea_ReturnsEmpty()
    {
        var (rt, world) = BuildRuntime();
        world.AddRoom(new Room("lf:1", "Room A1", "first") { Area = "lf-test" });

        var result = rt.Evaluate("tapestry.world.getRoomsInArea('nope').length;");

        Assert.Equal(0d, result);
    }

    [Fact]
    public void GetRoomArea_ReturnsArea_ForKnownRoom()
    {
        var (rt, world) = BuildRuntime();
        world.AddRoom(new Room("lf:1", "Room A1", "first") { Area = "lf-test" });

        var result = rt.Evaluate("tapestry.world.getRoomArea('lf:1');");

        Assert.Equal("lf-test", result);
    }

    [Fact]
    public void GetRoomArea_UnknownRoom_ReturnsNull()
    {
        var (rt, _) = BuildRuntime();

        var result = rt.Evaluate("tapestry.world.getRoomArea('ghost');");

        Assert.Null(result);
    }

    [Fact]
    public void TeleportEntity_MovesEntityIntoTargetRoom()
    {
        var (rt, world) = BuildRuntime();
        var roomA = new Room("A", "Room A", "start");
        var roomB = new Room("B", "Room B", "dest");
        world.AddRoom(roomA);
        world.AddRoom(roomB);
        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        roomA.AddEntity(entity);

        var result = rt.Evaluate($"tapestry.world.teleportEntity('{entity.Id}', 'B');");

        Assert.Equal(true, result);
        Assert.Equal("B", entity.LocationRoomId);
        Assert.DoesNotContain(roomA.Entities, e => e.Id == entity.Id);
        Assert.Contains(roomB.Entities, e => e.Id == entity.Id);
    }
}
