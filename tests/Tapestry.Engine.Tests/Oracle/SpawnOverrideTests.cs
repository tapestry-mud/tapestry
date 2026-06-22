using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Xunit;

namespace Tapestry.Engine.Tests.Oracle;

public class SpawnOverrideTests
{
    private static SpawnManager BuildManager(World world, EventBus bus)
    {
        var mgr = new SpawnManager(world, bus, new LootTableResolver(), new ItemRegistry());
        mgr.RegisterTemplate(new MobTemplate { Id = "hostile-melee", Name = "creature" });
        return mgr;
    }

    [Fact]
    public void SpawnMob_AppliesFrozenOverride_NameAndMaxHp()
    {
        var world = new World();
        world.AddRoom(new Room("oracle:r1", "Room", "desc"));
        var mgr = BuildManager(world, new EventBus());

        var over = new SpawnOverride { FromType = "m1", Name = "rot-touched wolf", MaxHp = 41, NoReroll = true };
        var entity = mgr.SpawnMob("hostile-melee", "oracle:r1", over);

        Assert.NotNull(entity);
        Assert.Equal("rot-touched wolf", entity!.Name);
        Assert.Equal(41, entity.Stats.BaseMaxHp);
        Assert.Equal(41, entity.Stats.Hp);
    }

    [Fact]
    public void RunAreaReset_ReappliesSameFrozenOverride()
    {
        var world = new World();
        world.AddRoom(new Room("oracle:r1", "Room", "desc"));
        var mgr = BuildManager(world, new EventBus());

        var over = new SpawnOverride { FromType = "m1", Name = "rot-touched wolf", MaxHp = 41, NoReroll = true };
        mgr.RegisterRoomSpawns("oracle", "oracle:r1",
            new[] { (Mob: "hostile-melee", Count: 1, Rare: (RareSpawnConfig?)null,
                     Tags: (IEnumerable<string>)new[] { "persistent" }, Override: (SpawnOverride?)over) },
            effectiveResetInterval: 0);

        mgr.RunAreaReset("oracle");
        var room = world.GetRoom("oracle:r1")!;
        var first = room.Entities.First(e => e.Name == "rot-touched wolf");
        room.RemoveEntity(first);
        world.UntrackEntity(first);
        mgr.RunAreaReset("oracle");

        var respawned = world.GetAllTrackedEntities().Single(e => e.Name == "rot-touched wolf");
        Assert.Equal(41, respawned.Stats.BaseMaxHp);
    }

    [Fact]
    public void RoomData_RoundTripsSpawnOverride()
    {
        var data = new Tapestry.Engine.Authoring.RoomData { Id = "oracle:r1", Area = "oracle" };
        data.Spawns.Add(new Tapestry.Engine.Authoring.RoomSpawnData
        {
            Base = "hostile-melee",
            Override = new Tapestry.Engine.Authoring.RoomSpawnOverrideData
            {
                FromType = "m1", Name = "rot-touched wolf", MaxHp = 41, Damage = "1d8"
            }
        });

        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(
                YamlDotNet.Serialization.DefaultValuesHandling.OmitNull | YamlDotNet.Serialization.DefaultValuesHandling.OmitEmptyCollections)
            .Build();
        var yaml = serializer.Serialize(data);

        var deser = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .Build();
        var round = deser.Deserialize<Tapestry.Engine.Authoring.RoomData>(yaml);

        Assert.Single(round.Spawns);
        Assert.Equal("hostile-melee", round.Spawns[0].Base);
        Assert.Equal(41, round.Spawns[0].Override!.MaxHp);
        Assert.Equal("rot-touched wolf", round.Spawns[0].Override!.Name);
    }
}
