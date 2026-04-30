using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class StackingModuleTests
{
    private static Entity MakePlayerWithItems(World world, params (string name, string? templateId, string? essence)[] items)
    {
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        foreach (var (name, templateId, essence) in items)
        {
            var item = new Entity("item", name);
            if (templateId != null) { item.SetProperty("template_id", templateId); }
            if (essence != null) { item.SetProperty("essence", essence); }
            player.AddToContents(item);
        }
        return player;
    }

    [Fact]
    public void GetStacks_StacksItemsWithSameTemplateId()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var world = provider.GetRequiredService<World>();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var player = MakePlayerWithItems(world,
            ("a potion", "core:potion", null),
            ("a potion", "core:potion", null));
        var result = rt.Evaluate($@"
            var stacks = tapestry.stacking.getStacks('{player.Id}');
            stacks.length;
        ");
        Assert.Equal(1, Convert.ToInt32(result));
    }

    [Fact]
    public void GetStacks_ReturnsQuantityAndName()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var world = provider.GetRequiredService<World>();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var player = MakePlayerWithItems(world,
            ("a potion", "core:potion", null),
            ("a potion", "core:potion", null));
        var result = rt.Evaluate($@"
            var stacks = tapestry.stacking.getStacks('{player.Id}');
            stacks[0].quantity + ':' + stacks[0].name;
        ");
        Assert.Equal("2:a potion", result!.ToString());
    }

    [Fact]
    public void AddKey_CausesItemsWithDifferentPropertyToSeparate()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var world = provider.GetRequiredService<World>();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var item1 = new Entity("item", "a sword");
        item1.SetProperty("template_id", "core:sword");
        item1.SetProperty("quality", "crude");
        var item2 = new Entity("item", "a sword");
        item2.SetProperty("template_id", "core:sword");
        item2.SetProperty("quality", "fine");
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        player.AddToContents(item1);
        player.AddToContents(item2);
        var result = rt.Evaluate($@"
            tapestry.stacking.addKey('quality');
            var stacks = tapestry.stacking.getStacks('{player.Id}');
            stacks.length;
        ");
        Assert.Equal(2, Convert.ToInt32(result));
    }
}
