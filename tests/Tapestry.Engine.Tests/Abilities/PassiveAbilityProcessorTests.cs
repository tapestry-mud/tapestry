using Tapestry.Engine.Abilities;

namespace Tapestry.Engine.Tests.Abilities;

public class PassiveAbilityProcessorTests
{
    private World _world = null!;
    private AbilityRegistry _registry = null!;
    private ProficiencyManager _proficiency = null!;
    private Entity _player = null!;
    private Random _seededRandom = null!;

    private void Setup()
    {
        _world = new World();
        _registry = new AbilityRegistry();
        _proficiency = new ProficiencyManager(_world, _registry);
        _seededRandom = new Random(42);
        _player = new Entity("player", "Travis");
        _player.Stats.BaseStrength = 10;
        _player.Stats.BaseDexterity = 10;
        _player.Stats.BaseMaxHp = 100;
        _player.Stats.Hp = 100;
        _world.TrackEntity(_player);
    }

    [Fact]
    public void CheckBinaryPassive_AtProficiency100_AlwaysFires()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "second_attack", Name = "Second Attack",
            Type = AbilityType.Passive, Category = AbilityCategory.Skill,
            Metadata = new Dictionary<string, object?> { ["passive_mode"] = "binary", ["hook"] = "extra_attack" }
        });
        _proficiency.Learn(_player.Id, "second_attack", 100);
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);
        Assert.True(processor.CheckBinaryPassive(_player.Id, "second_attack", _seededRandom));
    }

    [Fact]
    public void CheckBinaryPassive_AtProficiency1_RarelyFires()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "second_attack", Name = "Second Attack",
            Type = AbilityType.Passive, Category = AbilityCategory.Skill,
            Metadata = new Dictionary<string, object?> { ["passive_mode"] = "binary", ["hook"] = "extra_attack" }
        });
        _proficiency.Learn(_player.Id, "second_attack", 1);
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);
        var hits = 0;
        var random = new Random(42);
        for (var i = 0; i < 100; i++)
        {
            if (processor.CheckBinaryPassive(_player.Id, "second_attack", random))
            {
                hits++;
            }
        }
        Assert.True(hits < 10);
    }

    [Fact]
    public void CheckBinaryPassive_UnlearnedAbility_ReturnsFalse()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "dodge", Name = "Dodge",
            Type = AbilityType.Passive, Category = AbilityCategory.Skill
        });
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);
        Assert.False(processor.CheckBinaryPassive(_player.Id, "dodge", _seededRandom));
    }

    [Fact]
    public void GetScalingBonus_ScalesLinearly()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "enhanced_damage", Name = "Enhanced Damage",
            Type = AbilityType.Passive, Category = AbilityCategory.Skill,
            Metadata = new Dictionary<string, object?> { ["passive_mode"] = "scaling", ["hook"] = "stat_modifier", ["max_bonus"] = 50 }
        });
        _proficiency.Learn(_player.Id, "enhanced_damage", 80);
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);
        Assert.Equal(40, processor.GetScalingBonus(_player.Id, "enhanced_damage"));
    }

    [Fact]
    public void GetScalingBonus_AtProficiency100_ReturnsMaxBonus()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "enhanced_damage", Name = "Enhanced Damage",
            Type = AbilityType.Passive, Category = AbilityCategory.Skill,
            Metadata = new Dictionary<string, object?> { ["max_bonus"] = 50 }
        });
        _proficiency.Learn(_player.Id, "enhanced_damage", 100);
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);
        Assert.Equal(50, processor.GetScalingBonus(_player.Id, "enhanced_damage"));
    }

    [Fact]
    public void GetScalingBonus_UnlearnedAbility_Returns0()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "enhanced_damage", Name = "Enhanced Damage",
            Type = AbilityType.Passive, Category = AbilityCategory.Skill,
            Metadata = new Dictionary<string, object?> { ["max_bonus"] = 50 }
        });
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);
        Assert.Equal(0, processor.GetScalingBonus(_player.Id, "enhanced_damage"));
    }

    [Fact]
    public void GetExtraAttackCount_ChecksAllAttackPassives()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "second_attack", Name = "Second Attack",
            Type = AbilityType.Passive, Category = AbilityCategory.Skill,
            Metadata = new Dictionary<string, object?> { ["passive_mode"] = "binary", ["hook"] = "extra_attack" }
        });
        _registry.Register(new AbilityDefinition
        {
            Id = "third_attack", Name = "Third Attack",
            Type = AbilityType.Passive, Category = AbilityCategory.Skill,
            Metadata = new Dictionary<string, object?> { ["passive_mode"] = "binary", ["hook"] = "extra_attack" }
        });
        _proficiency.Learn(_player.Id, "second_attack", 100);
        _proficiency.Learn(_player.Id, "third_attack", 100);
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);
        Assert.Equal(2, processor.GetExtraAttackCount(_player.Id, _seededRandom));
    }

    [Fact]
    public void CheckDefensivePassive_At100Proficiency_FiresAbout40Percent()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "dodge", Name = "Dodge",
            Type = AbilityType.Passive, Category = AbilityCategory.Skill,
            MaxChance = 40,
            Metadata = new Dictionary<string, object?> { ["passive_mode"] = "binary", ["hook"] = "defensive_check" }
        });
        _proficiency.Learn(_player.Id, "dodge", 100);
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);
        var random = new Random(42);
        var hits = 0;
        for (var i = 0; i < 200; i++)
        {
            if (processor.CheckDefensivePassives(_player.Id, random) != null)
            {
                hits++;
            }
        }
        // At 100% proficiency with MaxChance=40, effective chance is 40%. Expect roughly 80/200.
        // Allow a wide range for randomness: 50-120
        Assert.True(hits > 50, $"Expected > 50 dodge successes but got {hits}");
        Assert.True(hits < 120, $"Expected < 120 dodge successes but got {hits}");
    }

    [Fact]
    public void CheckDefensivePassives_UsesMaxChanceFromDefinition()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "dodge",
            Name = "Dodge",
            Type = AbilityType.Passive,
            Category = AbilityCategory.Skill,
            MaxChance = 30,
            Metadata = new Dictionary<string, object?>
            {
                ["hook"] = "defensive_check",
                ["passive_mode"] = "binary"
            }
        });

        _proficiency.Learn(_player.Id, "dodge", 100);
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);

        var successes = 0;
        var trials = 10000;
        var random = new Random(42);
        for (var i = 0; i < trials; i++)
        {
            if (processor.CheckDefensivePassives(_player.Id, random) != null)
            {
                successes++;
            }
        }

        var rate = (double)successes / trials;
        Assert.InRange(rate, 0.25, 0.35); // ~30%, not ~40%
    }

    [Fact]
    public void CheckBinaryPassive_Variance50_FiresAtHalfRateVsMaxChance100()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "lf:parry",
            Name = "Parry",
            Type = AbilityType.Passive,
            Category = AbilityCategory.Skill,
            Variance = 50,
            MaxChance = 100,
            Metadata = new Dictionary<string, object?> { ["passive_mode"] = "binary", ["hook"] = "defensive_check" }
        });
        _proficiency.Learn(_player.Id, "lf:parry", 100);
        var processor = new PassiveAbilityProcessor(_registry, _proficiency);

        var hitCount = 0;
        var random = new Random(42);
        for (var i = 0; i < 200; i++)
        {
            if (processor.CheckBinaryPassive(_player.Id, "lf:parry", random))
            {
                hitCount++;
            }
        }
        // ~50% hit rate -- not 100%. Allow 60-140 out of 200.
        Assert.InRange(hitCount, 60, 140);
    }
}
