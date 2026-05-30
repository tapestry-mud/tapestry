using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class ItemsModuleTests
{
    private (JintRuntime rt, World world, ItemRegistry items) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider.GetRequiredService<ItemRegistry>());
    }

    private static void RegisterCoin(ItemRegistry items)
    {
        items.Register(new ItemTemplate
        {
            Id = "core:coin",
            Name = "a coin",
            Type = "item",
            Properties = new Dictionary<string, object?>(),
            Modifiers = new List<ItemTemplate.ModifierEntry>()
        });
    }

    [Fact]
    public void SpawnToContainer_PlacesAndTracksItemInContainer()
    {
        var (rt, world, items) = BuildRuntime();
        RegisterCoin(items);
        var room = new Room("test-room", "Room", "A room.");
        world.AddRoom(room);
        var chest = new Entity("container", "a chest");
        room.AddEntity(chest);
        world.TrackEntity(chest);

        rt.Evaluate($"tapestry.items.spawnToContainer('core:coin', '{chest.Id}')");

        chest.Contents.Should().ContainSingle(e => e.Name == "a coin");
        var coin = chest.Contents.Single();
        world.GetEntity(coin.Id).Should().NotBeNull(); // tracked → survives ticks
    }

    [Fact]
    public void SpawnToContainer_NonContainerTarget_ReturnsNullAndPlacesNothing()
    {
        var (rt, world, items) = BuildRuntime();
        RegisterCoin(items);
        var room = new Room("test-room", "Room", "A room.");
        world.AddRoom(room);
        var sack = new Entity("item", "a sack"); // type != container
        room.AddEntity(sack);
        world.TrackEntity(sack);

        var result = rt.Evaluate(
            $"tapestry.items.spawnToContainer('core:coin', '{sack.Id}') === null");

        ((bool)result!).Should().BeTrue();
        sack.Contents.Should().BeEmpty();
    }
}
