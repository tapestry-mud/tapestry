using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class WorldModuleTests
{
    private (JintRuntime rt, World world) BuildRuntime()
    {
        var (rt, world, _) = BuildRuntimeWithProvider();
        return (rt, world);
    }

    private (JintRuntime rt, World world, ServiceProvider provider) BuildRuntimeWithProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider);
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

    /// <summary>Register a mob + item template directly on the C# registries and
    /// seed live instances by tracking entities stamped with template_id (the
    /// same property GetEntitiesByTemplateId matches on).</summary>
    private static void SeedTemplates(ServiceProvider provider, World world)
    {
        provider.GetRequiredService<SpawnManager>().RegisterTemplate(
            new MobTemplate { Id = "test:goblin_scout", Name = "Goblin Scout" });
        provider.GetRequiredService<ItemRegistry>().Register(
            new ItemTemplate { Id = "test:rusty_sword", Name = "Rusty Sword" });

        for (var i = 0; i < 2; i++)
        {
            var mob = new Entity(EntityTypes.Npc, "Goblin Scout");
            mob.SetProperty(CommonProperties.TemplateId, "test:goblin_scout");
            world.TrackEntity(mob);
        }
    }

    [Fact]
    public void SearchTemplates_KeywordMatch_ReturnsKindAndLiveInstanceCounts()
    {
        var (rt, world, provider) = BuildRuntimeWithProvider();
        SeedTemplates(provider, world);

        rt.Execute("var mobs = tapestry.world.searchTemplates('gob');");
        Assert.Equal(1d, (double)rt.Evaluate("mobs.length")!);
        Assert.Equal("test:goblin_scout", rt.Evaluate("mobs[0].id"));
        Assert.Equal("Goblin Scout", rt.Evaluate("mobs[0].name"));
        Assert.Equal("mob", rt.Evaluate("mobs[0].kind"));
        Assert.Equal(2d, (double)rt.Evaluate("mobs[0].instances")!);

        rt.Execute("var items = tapestry.world.searchTemplates('rusty');");
        Assert.Equal(1d, (double)rt.Evaluate("items.length")!);
        Assert.Equal("test:rusty_sword", rt.Evaluate("items[0].id"));
        Assert.Equal("item", rt.Evaluate("items[0].kind"));
        Assert.Equal(0d, (double)rt.Evaluate("items[0].instances")!);
    }

    [Fact]
    public void SearchTemplates_All_ReturnsEveryTemplate()
    {
        var (rt, world, provider) = BuildRuntimeWithProvider();
        SeedTemplates(provider, world);

        rt.Execute("var r = tapestry.world.searchTemplates('ALL');");

        Assert.True((double)rt.Evaluate("r.length")! >= 2d);
        rt.Execute(
            "function pickId(arr, id) {" +
            "  for (var i = 0; i < arr.length; i++) {" +
            "    if (arr[i].id === id) { return arr[i]; }" +
            "  }" +
            "  return null;" +
            "}");
        Assert.Equal("mob", rt.Evaluate("pickId(r, 'test:goblin_scout').kind"));
        Assert.Equal("item", rt.Evaluate("pickId(r, 'test:rusty_sword').kind"));
    }

    [Fact]
    public void SearchTemplates_EmptyOrWhitespaceKeyword_ReturnsEmptyArray()
    {
        var (rt, world, provider) = BuildRuntimeWithProvider();
        SeedTemplates(provider, world);

        Assert.Equal(0d, (double)rt.Evaluate("tapestry.world.searchTemplates('').length")!);
        Assert.Equal(0d, (double)rt.Evaluate("tapestry.world.searchTemplates('   ').length")!);
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

    [Fact]
    public void GetNpcsInWorld_ReturnsOnlyNpcs_WithIdNameRoomId()
    {
        var (rt, world) = BuildRuntime();
        var (npc, _, _) = SeedWorld(world);

        rt.Execute("var r = tapestry.world.getNpcsInWorld();");

        Assert.Equal(1d, (double)rt.Evaluate("r.length")!);
        Assert.Equal(npc.Id.ToString(), rt.Evaluate("r[0].id"));
        Assert.Equal("Goblin Scout", rt.Evaluate("r[0].name"));
        Assert.Equal("test:lair", rt.Evaluate("r[0].roomId"));
    }

    [Fact]
    public void GetItemsInWorld_ReturnsOnlyItems_WithIdNameRoomId()
    {
        var (rt, world) = BuildRuntime();
        var (_, item, _) = SeedWorld(world);

        rt.Execute("var r = tapestry.world.getItemsInWorld();");

        Assert.Equal(1d, (double)rt.Evaluate("r.length")!);
        Assert.Equal(item.Id.ToString(), rt.Evaluate("r[0].id"));
        Assert.Equal("goblin ear", rt.Evaluate("r[0].name"));
        Assert.Equal("test:lair", rt.Evaluate("r[0].roomId"));
    }

    [Fact]
    public void GetNpcsInWorld_ExcludesPlayersAndItems()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);

        rt.Execute("var r = tapestry.world.getNpcsInWorld();");

        // Only the one npc; no player, no item
        Assert.Equal(1d, (double)rt.Evaluate("r.length")!);
        Assert.Equal("Goblin Scout", rt.Evaluate("r[0].name"));
    }

    [Fact]
    public void GetItemsInWorld_ExcludesPlayersAndNpcs()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);

        rt.Execute("var r = tapestry.world.getItemsInWorld();");

        // Only the one item; no player, no npc
        Assert.Equal(1d, (double)rt.Evaluate("r.length")!);
        Assert.Equal("goblin ear", rt.Evaluate("r[0].name"));
    }
}
