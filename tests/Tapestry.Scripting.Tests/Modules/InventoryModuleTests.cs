using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class InventoryModuleTests
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
    public void GetAll_SkipsItemsTaggedNoGet()
    {
        var (rt, world) = BuildRuntime();

        var room = new Room("test-room", "Test Room", "A plain room.");
        world.AddRoom(room);

        var player = new Entity("player", "Rand");
        room.AddEntity(player);
        world.TrackEntity(player);

        var sword = new Entity("item:weapon", "a sword");
        sword.AddTag("item");
        room.AddEntity(sword);
        world.TrackEntity(sword);

        var statue = new Entity("item:decoration", "a stone statue");
        statue.AddTag("item");
        statue.AddTag("no_get");
        room.AddEntity(statue);
        world.TrackEntity(statue);

        var playerId = player.Id.ToString();
        var result = rt.Evaluate($"tapestry.inventory.getAll('{playerId}', 'all')");

        // The returned array should contain only the sword, not the statue
        var resultLength = rt.Evaluate($"tapestry.inventory.getAll('{playerId}', 'all').length");
        // sword is already picked up by the first call; reload by checking room state
        player.Contents.Should().Contain(sword);
        room.Entities.Should().Contain(statue);
        room.Entities.Should().NotContain(sword);
    }

    [Fact]
    public void GetAll_NoGetItemStaysInRoom()
    {
        var (rt, world) = BuildRuntime();

        var room = new Room("test-room2", "Test Room 2", "Another plain room.");
        world.AddRoom(room);

        var player = new Entity("player", "Mat");
        room.AddEntity(player);
        world.TrackEntity(player);

        var fixture = new Entity("item:fixture", "a heavy anvil");
        fixture.AddTag("item");
        fixture.AddTag("no_get");
        room.AddEntity(fixture);
        world.TrackEntity(fixture);

        var playerId = player.Id.ToString();
        rt.Evaluate($"tapestry.inventory.getAll('{playerId}', 'all')");

        player.Contents.Should().BeEmpty();
        room.Entities.Should().Contain(fixture);
    }
}
