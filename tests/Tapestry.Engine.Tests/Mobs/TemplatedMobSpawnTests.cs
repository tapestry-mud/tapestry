using Tapestry.Engine.Classes;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Mobs;

public class TemplatedMobSpawnTests
{
    private World _world = null!;
    private EventBus _bus = null!;
    private SpawnManager _spawner = null!;
    private ClassRegistry _classes = null!;
    private RaceRegistry _races = null!;

    private void Setup()
    {
        _world = new World();
        _bus = new EventBus();
        _classes = new ClassRegistry();
        _races = new RaceRegistry();
        var itemReg = new ItemRegistry();
        var lootResolver = new LootTableResolver();
        _spawner = new SpawnManager(_world, _bus, lootResolver, itemReg, _classes, _races);

        _world.AddRoom(new Room("room1", "Room", "desc"));
    }

    [Fact]
    public void SpawnMob_WithClassAndLevel_AppliesAveragedGrowth()
    {
        Setup();
        _classes.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            StatGrowth = new Dictionary<StatType, string> { { StatType.MaxHp, "2d6+2" } }
        });

        var template = new MobTemplate
        {
            Id = "test-mob",
            Name = "Elf Warrior",
            Type = "npc",
            Stats = new MobTemplateStats { MaxHp = 10 },
            Class = "warrior",
            Level = 5
        };
        _spawner.RegisterTemplate(template);

        var entity = _spawner.SpawnMob("test-mob", "room1");

        Assert.NotNull(entity);
        // 2d6+2 avg per level = 9. Level 5 = 45 added on top of base 10.
        Assert.Equal(55, entity!.Stats.BaseMaxHp);
    }

    [Fact]
    public void SpawnMob_WithRace_AppliesRacialFlagsAsTags()
    {
        Setup();
        _races.Register(new RaceDefinition
        {
            Id = "elf",
            Name = "Elf",
            RacialFlags = new List<string> { "resist_poison", "regen" }
        });

        var template = new MobTemplate
        {
            Id = "test-mob", Name = "Elf Warrior", Type = "npc",
            Stats = new MobTemplateStats { MaxHp = 50 },
            Race = "elf"
        };
        _spawner.RegisterTemplate(template);

        var entity = _spawner.SpawnMob("test-mob", "room1");
        Assert.NotNull(entity);
        Assert.True(entity!.HasTag("resist_poison"));
        Assert.True(entity.HasTag("regen"));
        Assert.Equal("elf", entity.GetProperty<string>("race"));
    }

    [Fact]
    public void SpawnMob_WithoutClass_UsesTemplateStatsDirectly()
    {
        Setup();
        var template = new MobTemplate
        {
            Id = "dummy", Name = "Dummy", Type = "npc",
            Stats = new MobTemplateStats { MaxHp = 40 }
        };
        _spawner.RegisterTemplate(template);

        var entity = _spawner.SpawnMob("dummy", "room1");
        Assert.NotNull(entity);
        Assert.Equal(40, entity!.Stats.BaseMaxHp);
    }
}
