using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class WorldModuleTests
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

    /// <summary>Two rooms; a goblin npc + goblin item in the lair, a player in the temple.</summary>
    private static (Entity npc, Entity item, Entity player) SeedWorld(World world)
    {
        world.AddRoom(new Room("test:lair", "Goblin Lair", "A dank lair."));
        world.AddRoom(new Room("test:temple", "Quiet Temple", "A calm temple."));

        var npc = new Entity(EntityTypes.Npc, "Goblin Scout");
        npc.LocationRoomId = "test:lair";
        npc.SetProperty("template_id", "test:goblin_scout");
        world.TrackEntity(npc);

        var item = new Entity(EntityTypes.Item, "goblin ear");
        item.LocationRoomId = "test:lair";
        world.TrackEntity(item);

        var player = new Entity(EntityTypes.Player, "Hero");
        player.LocationRoomId = "test:temple";
        world.TrackEntity(player);

        return (npc, item, player);
    }

    /// <summary>Result arrays are CLR-wrapped (no Array.prototype), so tests select
    /// entries with an indexed-loop helper instead of .filter().</summary>
    private static void DefinePickHelper(JintRuntime rt)
    {
        rt.Execute(
            "function pick(arr, type) {" +
            "  for (var i = 0; i < arr.length; i++) {" +
            "    if (arr[i].type === type) { return arr[i]; }" +
            "  }" +
            "  return null;" +
            "}");
    }

    [Fact]
    public void FindEntitiesByName_CaseInsensitive_ReturnsRoomIdAndRoomName()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);
        DefinePickHelper(rt);

        rt.Execute("var r = tapestry.world.findEntitiesByName('GOB');");

        Assert.Equal(2d, (double)rt.Evaluate("r.length")!);
        Assert.Equal("Goblin Scout", rt.Evaluate("pick(r, 'npc').name"));
        Assert.Equal("test:lair", rt.Evaluate("pick(r, 'npc').roomId"));
        Assert.Equal("Goblin Lair", rt.Evaluate("pick(r, 'npc').roomName"));
        Assert.Equal("test:goblin_scout", rt.Evaluate("pick(r, 'npc').templateId"));
        Assert.Equal("goblin ear", rt.Evaluate("pick(r, 'item').name"));
        Assert.Equal("test:lair", rt.Evaluate("pick(r, 'item').roomId"));
    }

    [Fact]
    public void FindEntitiesByName_NoMatch_ReturnsEmptyArray()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);

        Assert.Equal(0d, (double)rt.Evaluate("tapestry.world.findEntitiesByName('zzz').length")!);
    }

    [Fact]
    public void FindEntitiesByName_EmptyOrWhitespaceKeyword_ReturnsEmptyArray()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);

        Assert.Equal(0d, (double)rt.Evaluate("tapestry.world.findEntitiesByName('').length")!);
        Assert.Equal(0d, (double)rt.Evaluate("tapestry.world.findEntitiesByName('   ').length")!);
    }

    [Fact]
    public void FindEntitiesByName_CarriedItem_ShowsHolderAndHolderRoom()
    {
        var (rt, world) = BuildRuntime();
        var (_, item, player) = SeedWorld(world);
        player.AddToContents(item);

        rt.Execute("var r = tapestry.world.findEntitiesByName('goblin ear');");

        Assert.Equal(1d, (double)rt.Evaluate("r.length")!);
        Assert.Equal("Hero", rt.Evaluate("r[0].holderName"));
        // Carried item has no LocationRoomId of its own; the room resolves
        // through the holder's container chain.
        Assert.Equal("test:temple", rt.Evaluate("r[0].roomId"));
        Assert.Equal("Quiet Temple", rt.Evaluate("r[0].roomName"));
    }

    [Fact]
    public void FindEntitiesByName_UncarriedRoomlessEntity_ReturnsEmptyRoomFields()
    {
        var (rt, world) = BuildRuntime();
        var limbo = new Entity(EntityTypes.Item, "limbo trinket");
        world.TrackEntity(limbo);

        rt.Execute("var r = tapestry.world.findEntitiesByName('limbo');");

        Assert.Equal(1d, (double)rt.Evaluate("r.length")!);
        Assert.Equal("", rt.Evaluate("r[0].roomId"));
        Assert.Equal("", rt.Evaluate("r[0].roomName"));
        Assert.Equal("", rt.Evaluate("r[0].holderName"));
    }
}
