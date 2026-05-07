using Tapestry.Engine.Classes;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Training;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Classes;

public class StatGrowthOnLevelUpTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private ProgressionManager _progression = null!;
    private ClassRegistry _classRegistry = null!;
    private TrainingManager _trainingManager = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _classRegistry = new ClassRegistry();
        _progression = new ProgressionManager(_world, _eventBus);
        _trainingManager = new TrainingManager(_world, null!, new RaceRegistry(), new TrainingConfig(), new Tapestry.Engine.Abilities.AbilityRegistry());

        StatGrowthOnLevelUp.Subscribe(_eventBus, _world, _classRegistry, _trainingManager, new Random(42));

        _progression.RegisterTrack(new TrackDefinition
        {
            Name = "combat",
            MaxLevel = 10,
            XpFormula = lvl => 100 * lvl
        });
    }

    private Entity CreatePlayerWithClass(string classId)
    {
        var e = new Entity("player", "Tester");
        e.AddTag("player");
        e.Stats.BaseMaxHp = 100;
        e.Stats.Hp = 100;
        e.SetProperty("class", classId);
        _world.TrackEntity(e);
        return e;
    }

    [Fact]
    public void LevelUp_AppliesStatGrowthFromClass()
    {
        Setup();
        _classRegistry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            StatGrowth = new Dictionary<StatType, string>
            {
                { StatType.MaxHp, "2d6+2" }
            }
        });
        var player = CreatePlayerWithClass("warrior");
        var startHp = player.Stats.BaseMaxHp;

        _progression.GrantExperience(player.Id, 250, "combat", "test");

        Assert.True(player.Stats.BaseMaxHp > startHp, $"MaxHp should grow, was {startHp} now {player.Stats.BaseMaxHp}");
        // 2d6+2 range per level-up: min 4, max 14. One level-up = 1 to 2 levels here; just check bounds:
        var delta = player.Stats.BaseMaxHp - startHp;
        Assert.InRange(delta, 4, 28);
    }

    [Fact]
    public void LevelUp_WithoutClass_NoStatChange()
    {
        Setup();
        var e = new Entity("player", "Classless");
        e.AddTag("player");
        e.Stats.BaseMaxHp = 100;
        _world.TrackEntity(e);
        _progression.GrantExperience(e.Id, 250, "combat", "test");
        Assert.Equal(100, e.Stats.BaseMaxHp);
    }

    [Fact]
    public void LevelUp_ClassHasNoGrowthEntry_NoStatChange()
    {
        Setup();
        _classRegistry.Register(new ClassDefinition { Id = "empty", Name = "Empty" });
        var player = CreatePlayerWithClass("empty");
        _progression.GrantExperience(player.Id, 250, "combat", "test");
        Assert.Equal(100, player.Stats.BaseMaxHp);
    }

    [Fact]
    public void LevelUp_GrantsTrainsPerLevel()
    {
        Setup();
        _classRegistry.Register(new ClassDefinition
        {
            Id = "bm", Name = "Warrior",
            StatGrowth = new Dictionary<StatType, string> { { StatType.MaxHp, "1d1" } },
            TrainsPerLevel = 5
        });
        var player = CreatePlayerWithClass("bm");

        _progression.GrantExperience(player.Id, 250, "combat", "test");

        Assert.Equal(5, _trainingManager.GetTrainsAvailable(player.Id));
    }

    [Fact]
    public void LevelUp_StatAffectedGrowth_AddsBonus()
    {
        Setup();
        _classRegistry.Register(new ClassDefinition
        {
            Id = "bm2", Name = "Warrior2",
            StatGrowth = new Dictionary<StatType, string> { { StatType.MaxHp, "1d1" } },
            GrowthBonuses = new Dictionary<StatType, StatType>
            {
                { StatType.MaxHp, StatType.Constitution }
            }
        });
        var player = CreatePlayerWithClass("bm2");
        player.Stats.BaseConstitution = 20;
        var startHp = player.Stats.BaseMaxHp;

        _progression.GrantExperience(player.Id, 250, "combat", "test");

        var delta = player.Stats.BaseMaxHp - startHp;
        Assert.True(delta >= 6, $"Expected at least 6 HP gain, got {delta}");
    }

    [Fact]
    public void LevelUp_StatAffectedGrowth_PureDiceWhenNoBonus()
    {
        Setup();
        _classRegistry.Register(new ClassDefinition
        {
            Id = "bm3", Name = "Warrior3",
            StatGrowth = new Dictionary<StatType, string> { { StatType.MaxHp, "1d1" } }
        });
        var player = CreatePlayerWithClass("bm3");
        player.Stats.BaseConstitution = 20;
        var startHp = player.Stats.BaseMaxHp;

        _progression.GrantExperience(player.Id, 250, "combat", "test");

        var delta = player.Stats.BaseMaxHp - startHp;
        Assert.InRange(delta, 1, 4);
    }
}
