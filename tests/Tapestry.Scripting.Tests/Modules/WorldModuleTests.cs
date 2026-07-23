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

    /// <summary>Inline pick helper JS: find first entry whose .type matches.</summary>
    private static string Pick(string arrExpr, string type) =>
        $"(function(arr){{ for(var i=0;i<arr.length;i++){{ if(arr[i].type==='{type}')return arr[i]; }} return null; }})({arrExpr})";

    [Fact]
    public void FindEntitiesByName_CaseInsensitive_ReturnsRoomIdAndRoomName()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);

        var findExpr = "tapestry.world.findEntitiesByName('GOB')";
        Assert.Equal(2d, (double)EsmTest.Eval(rt, $"{findExpr}.length")!);
        Assert.Equal("Goblin Scout", EsmTest.Eval(rt, $"{Pick(findExpr, "npc")}.name"));
        Assert.Equal("test:lair", EsmTest.Eval(rt, $"{Pick(findExpr, "npc")}.roomId"));
        Assert.Equal("Goblin Lair", EsmTest.Eval(rt, $"{Pick(findExpr, "npc")}.roomName"));
        Assert.Equal("test:goblin_scout", EsmTest.Eval(rt, $"{Pick(findExpr, "npc")}.templateId"));
        Assert.Equal("goblin ear", EsmTest.Eval(rt, $"{Pick(findExpr, "item")}.name"));
        Assert.Equal("test:lair", EsmTest.Eval(rt, $"{Pick(findExpr, "item")}.roomId"));
    }

    [Fact]
    public void FindEntitiesByName_NoMatch_ReturnsEmptyArray()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);

        Assert.Equal(0d, (double)EsmTest.Eval(rt, "tapestry.world.findEntitiesByName('zzz').length")!);
    }

    [Fact]
    public void FindEntitiesByName_EmptyOrWhitespaceKeyword_ReturnsEmptyArray()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);

        Assert.Equal(0d, (double)EsmTest.Eval(rt, "tapestry.world.findEntitiesByName('').length")!);
        Assert.Equal(0d, (double)EsmTest.Eval(rt, "tapestry.world.findEntitiesByName('   ').length")!);
    }

    [Fact]
    public void FindEntitiesByName_CarriedItem_ShowsHolderAndHolderRoom()
    {
        var (rt, world) = BuildRuntime();
        var (_, item, player) = SeedWorld(world);
        player.AddToContents(item);

        var findExpr = "tapestry.world.findEntitiesByName('goblin ear')";
        Assert.Equal(1d, (double)EsmTest.Eval(rt, $"{findExpr}.length")!);
        Assert.Equal("Hero", EsmTest.Eval(rt, $"{findExpr}[0].holderName"));
        // Carried item has no LocationRoomId of its own; the room resolves
        // through the holder's container chain.
        Assert.Equal("test:temple", EsmTest.Eval(rt, $"{findExpr}[0].roomId"));
        Assert.Equal("Quiet Temple", EsmTest.Eval(rt, $"{findExpr}[0].roomName"));
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

    /// <summary>Inline pickId helper JS: find first entry whose .id matches.</summary>
    private static string PickId(string arrExpr, string id) =>
        $"(function(arr){{ for(var i=0;i<arr.length;i++){{ if(arr[i].id==='{id}')return arr[i]; }} return null; }})({arrExpr})";

    [Fact]
    public void SearchTemplates_KeywordMatch_ReturnsKindAndLiveInstanceCounts()
    {
        var (rt, world, provider) = BuildRuntimeWithProvider();
        SeedTemplates(provider, world);

        var mobsExpr = "tapestry.world.searchTemplates('gob')";
        Assert.Equal(1d, (double)EsmTest.Eval(rt, $"{mobsExpr}.length")!);
        Assert.Equal("test:goblin_scout", EsmTest.Eval(rt, $"{mobsExpr}[0].id"));
        Assert.Equal("Goblin Scout", EsmTest.Eval(rt, $"{mobsExpr}[0].name"));
        Assert.Equal("mob", EsmTest.Eval(rt, $"{mobsExpr}[0].kind"));
        Assert.Equal(2d, (double)EsmTest.Eval(rt, $"{mobsExpr}[0].instances")!);

        var itemsExpr = "tapestry.world.searchTemplates('rusty')";
        Assert.Equal(1d, (double)EsmTest.Eval(rt, $"{itemsExpr}.length")!);
        Assert.Equal("test:rusty_sword", EsmTest.Eval(rt, $"{itemsExpr}[0].id"));
        Assert.Equal("item", EsmTest.Eval(rt, $"{itemsExpr}[0].kind"));
        Assert.Equal(0d, (double)EsmTest.Eval(rt, $"{itemsExpr}[0].instances")!);
    }

    [Fact]
    public void SearchTemplates_All_ReturnsEveryTemplate()
    {
        var (rt, world, provider) = BuildRuntimeWithProvider();
        SeedTemplates(provider, world);

        var allExpr = "tapestry.world.searchTemplates('ALL')";
        Assert.True((double)EsmTest.Eval(rt, $"{allExpr}.length")! >= 2d);
        Assert.Equal("mob", EsmTest.Eval(rt, $"{PickId(allExpr, "test:goblin_scout")}.kind"));
        Assert.Equal("item", EsmTest.Eval(rt, $"{PickId(allExpr, "test:rusty_sword")}.kind"));
    }

    [Fact]
    public void SearchTemplates_EmptyOrWhitespaceKeyword_ReturnsEmptyArray()
    {
        var (rt, world, provider) = BuildRuntimeWithProvider();
        SeedTemplates(provider, world);

        Assert.Equal(0d, (double)EsmTest.Eval(rt, "tapestry.world.searchTemplates('').length")!);
        Assert.Equal(0d, (double)EsmTest.Eval(rt, "tapestry.world.searchTemplates('   ').length")!);
    }

    [Fact]
    public void FindEntitiesByName_UncarriedRoomlessEntity_ReturnsEmptyRoomFields()
    {
        var (rt, world) = BuildRuntime();
        var limbo = new Entity(EntityTypes.Item, "limbo trinket");
        world.TrackEntity(limbo);

        var findExpr = "tapestry.world.findEntitiesByName('limbo')";
        Assert.Equal(1d, (double)EsmTest.Eval(rt, $"{findExpr}.length")!);
        Assert.Equal("", EsmTest.Eval(rt, $"{findExpr}[0].roomId"));
        Assert.Equal("", EsmTest.Eval(rt, $"{findExpr}[0].roomName"));
        Assert.Equal("", EsmTest.Eval(rt, $"{findExpr}[0].holderName"));
    }

    [Fact]
    public void GetNpcsInWorld_ReturnsOnlyNpcs_WithIdNameRoomId()
    {
        var (rt, world) = BuildRuntime();
        var (npc, _, _) = SeedWorld(world);

        var npcsExpr = "tapestry.world.getNpcsInWorld()";
        Assert.Equal(1d, (double)EsmTest.Eval(rt, $"{npcsExpr}.length")!);
        Assert.Equal(npc.Id.ToString(), EsmTest.Eval(rt, $"{npcsExpr}[0].id"));
        Assert.Equal("Goblin Scout", EsmTest.Eval(rt, $"{npcsExpr}[0].name"));
        Assert.Equal("test:lair", EsmTest.Eval(rt, $"{npcsExpr}[0].roomId"));
    }

    [Fact]
    public void GetItemsInWorld_ReturnsOnlyItems_WithIdNameRoomId()
    {
        var (rt, world) = BuildRuntime();
        var (_, item, _) = SeedWorld(world);

        var itemsExpr = "tapestry.world.getItemsInWorld()";
        Assert.Equal(1d, (double)EsmTest.Eval(rt, $"{itemsExpr}.length")!);
        Assert.Equal(item.Id.ToString(), EsmTest.Eval(rt, $"{itemsExpr}[0].id"));
        Assert.Equal("goblin ear", EsmTest.Eval(rt, $"{itemsExpr}[0].name"));
        Assert.Equal("test:lair", EsmTest.Eval(rt, $"{itemsExpr}[0].roomId"));
    }

    [Fact]
    public void GetNpcsInWorld_ExcludesPlayersAndItems()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);

        var npcsExpr = "tapestry.world.getNpcsInWorld()";
        // Only the one npc; no player, no item
        Assert.Equal(1d, (double)EsmTest.Eval(rt, $"{npcsExpr}.length")!);
        Assert.Equal("Goblin Scout", EsmTest.Eval(rt, $"{npcsExpr}[0].name"));
    }

    [Fact]
    public void GetItemsInWorld_ExcludesPlayersAndNpcs()
    {
        var (rt, world) = BuildRuntime();
        SeedWorld(world);

        var itemsExpr = "tapestry.world.getItemsInWorld()";
        // Only the one item; no player, no npc
        Assert.Equal(1d, (double)EsmTest.Eval(rt, $"{itemsExpr}.length")!);
        Assert.Equal("goblin ear", EsmTest.Eval(rt, $"{itemsExpr}[0].name"));
    }

    [Fact]
    public void ResetArea_InvokesSpawnManagerRunAreaReset()
    {
        var (rt, world, provider) = BuildRuntimeWithProvider();
        world.AddRoom(new Room("test:lair", "Goblin Lair", "A dank lair."));

        var spawnManager = provider.GetRequiredService<SpawnManager>();
        spawnManager.RegisterTemplate(new MobTemplate { Id = "test:goblin_scout", Name = "Goblin Scout" });
        spawnManager.RegisterAreaSpawns(new AreaSpawnConfig
        {
            Area = "test-area",
            ResetInterval = 300,
            Spawns = new List<SpawnRule>
            {
                new() { Room = "test:lair", Mob = "test:goblin_scout", Count = 2 }
            }
        });

        EsmTest.Eval(rt, "tapestry.world.resetArea('test-area')");

        var mobs = world.GetRoom("test:lair")!.Entities.Where(e => e.Type == "npc").ToList();
        Assert.Equal(2, mobs.Count);
    }

    [Fact]
    public void ResetArea_EmptyOrWhitespaceAreaId_DoesNotThrow()
    {
        var (rt, world) = BuildRuntime();

        EsmTest.Eval(rt, "tapestry.world.resetArea('')");
        EsmTest.Eval(rt, "tapestry.world.resetArea('   ')");
    }
}
