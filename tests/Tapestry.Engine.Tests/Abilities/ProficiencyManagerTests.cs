using Tapestry.Engine.Abilities;

namespace Tapestry.Engine.Tests.Abilities;

public class ProficiencyManagerTests
{
    private World _world = null!;
    private AbilityRegistry _registry = null!;
    private ProficiencyManager _proficiency = null!;
    private Entity _player = null!;

    private void Setup()
    {
        _world = new World();
        _registry = new AbilityRegistry();
        _proficiency = new ProficiencyManager(_world, _registry);

        _registry.Register(new AbilityDefinition
        {
            Id = "kick",
            Name = "Kick",
            Type = AbilityType.Active,
            Category = AbilityCategory.Skill,
            ProficiencyGainChance = 1.0
        });

        _registry.Register(new AbilityDefinition
        {
            Id = "fireball",
            Name = "Fireball",
            Type = AbilityType.Active,
            Category = AbilityCategory.Spell,
            ProficiencyGainChance = 0.0
        });

        _player = new Entity("player", "Travis");
        _world.TrackEntity(_player);
    }

    [Fact]
    public void Learn_SetsProficiencyTo1()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        Assert.Equal(1, _proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void Learn_WithInitialValue_SetsProficiency()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick", 60);
        Assert.Equal(60, _proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void GetProficiency_UnlearnedAbility_ReturnsNull()
    {
        Setup();
        Assert.Null(_proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void HasAbility_ReturnsTrueWhenLearned()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        Assert.True(_proficiency.HasAbility(_player.Id, "kick"));
    }

    [Fact]
    public void HasAbility_ReturnsFalseWhenNotLearned()
    {
        Setup();
        Assert.False(_proficiency.HasAbility(_player.Id, "kick"));
    }

    [Fact]
    public void Forget_RemovesProficiency()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        _proficiency.Forget(_player.Id, "kick");
        Assert.False(_proficiency.HasAbility(_player.Id, "kick"));
        Assert.Null(_proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void SetProficiency_UpdatesValue()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        _proficiency.SetProficiency(_player.Id, "kick", 50);
        Assert.Equal(50, _proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void SetProficiency_ClampsTo100()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        _proficiency.SetProficiency(_player.Id, "kick", 150);
        Assert.Equal(100, _proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void SetProficiency_ClampsTo1()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        _proficiency.SetProficiency(_player.Id, "kick", 0);
        Assert.Equal(1, _proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void IncreaseProficiency_RespectsCapAndMax()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        _proficiency.IncreaseProficiency(_player.Id, "kick", 50, cap: 30);
        Assert.Equal(30, _proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void RollProficiencyGain_With100PercentChance_Increases()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        var seededRandom = new Random(42);
        _proficiency.RollProficiencyGain(_player.Id, "kick", seededRandom);
        Assert.Equal(2, _proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void RollProficiencyGain_With0PercentChance_DoesNotIncrease()
    {
        Setup();
        _proficiency.Learn(_player.Id, "fireball");
        _proficiency.RollProficiencyGain(_player.Id, "fireball", new Random(42));
        Assert.Equal(1, _proficiency.GetProficiency(_player.Id, "fireball"));
    }

    [Fact]
    public void RollProficiencyGain_AtMax100_DoesNotIncrease()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick", 100);
        _proficiency.RollProficiencyGain(_player.Id, "kick", new Random(42));
        Assert.Equal(100, _proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void RollProficiencyGain_StopsAtCap()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        _proficiency.SetProficiency(_player.Id, "kick", 25);
        _registry.Register(new AbilityDefinition { Id = "kick", Name = "Kick", ProficiencyGainChance = 1.0 });
        _proficiency.RollProficiencyGain(_player.Id, "kick", new Random(42));
        Assert.Equal(25, _proficiency.GetProficiency(_player.Id, "kick"));
    }

    [Fact]
    public void RollProficiencyGain_FailurePathAppliesMultiplier()
    {
        Setup();
        _registry.Register(new AbilityDefinition
        {
            Id = "nullfail", Name = "NullFail",
            ProficiencyGainChance = 1.0,
            FailureProficiencyGainMultiplier = 0.0
        });
        var entity = new Entity("player", "P2");
        _world.TrackEntity(entity);
        _proficiency.Learn(entity.Id, "nullfail");
        _proficiency.RollProficiencyGain(entity.Id, "nullfail", new Random(42), wasFailure: true);
        Assert.Equal(1, _proficiency.GetProficiency(entity.Id, "nullfail"));
    }

    [Fact]
    public void RollProficiencyGain_NoCapPropertyDefaultsTo100()
    {
        Setup();
        _player.SetProperty(AbilityProperties.Proficiency("kick"), 99);
        _proficiency.RollProficiencyGain(_player.Id, "kick", new Random(42));
        Assert.InRange(_proficiency.GetProficiency(_player.Id, "kick")!.Value, 99, 100);
    }

    [Fact]
    public void Learn_SetsCap25OnFirstLearn()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        var capKey = AbilityProperties.Cap("kick");
        var cap = _player.GetAllProperties()[capKey];
        Assert.Equal(25, cap);
    }

    [Fact]
    public void Learn_DoesNotOverwriteExistingCap()
    {
        Setup();
        _player.SetProperty(AbilityProperties.Cap("kick"), 50);
        _proficiency.Learn(_player.Id, "kick");
        var cap = _player.GetAllProperties()[AbilityProperties.Cap("kick")];
        Assert.Equal(50, cap);
    }

    [Fact]
    public void GetCap_ReturnsNoviceAfterLearn()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        Assert.Equal(25, _proficiency.GetCap(_player.Id, "kick"));
    }

    [Fact]
    public void GetCap_Returns100WhenPropertyAbsent()
    {
        Setup();
        _player.SetProperty(AbilityProperties.Proficiency("kick"), 40);
        Assert.Equal(100, _proficiency.GetCap(_player.Id, "kick"));
    }

    [Fact]
    public void SetCap_WritesCapProperty()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        _proficiency.SetCap(_player.Id, "kick", 50);
        Assert.Equal(50, _proficiency.GetCap(_player.Id, "kick"));
    }

    [Fact]
    public void GetLearnedAbilities_ReturnsAllLearned()
    {
        Setup();
        _proficiency.Learn(_player.Id, "kick");
        _proficiency.Learn(_player.Id, "fireball", 50);
        var learned = _proficiency.GetLearnedAbilities(_player.Id);
        Assert.Equal(2, learned.Count);
        Assert.Contains(learned, l => l.AbilityId == "kick" && l.Proficiency == 1);
        Assert.Contains(learned, l => l.AbilityId == "fireball" && l.Proficiency == 50);
    }
}
