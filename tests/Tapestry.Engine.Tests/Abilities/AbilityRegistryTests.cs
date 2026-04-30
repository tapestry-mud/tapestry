using Tapestry.Engine.Abilities;

namespace Tapestry.Engine.Tests.Abilities;

public class AbilityRegistryTests
{
    private AbilityRegistry _registry = null!;

    private void Setup()
    {
        _registry = new AbilityRegistry();
    }

    private AbilityDefinition MakeAbility(string id, string name = "Test",
        AbilityType type = AbilityType.Active, AbilityCategory category = AbilityCategory.Skill,
        int priority = 0, string packName = "core")
    {
        return new AbilityDefinition
        {
            Id = id,
            Name = name,
            Type = type,
            Category = category,
            Priority = priority,
            PackName = packName
        };
    }

    [Fact]
    public void Register_And_Get_ReturnsDefinition()
    {
        Setup();
        var ability = MakeAbility("kick", "Kick");
        _registry.Register(ability);
        var result = _registry.Get("kick");
        Assert.NotNull(result);
        Assert.Equal("Kick", result!.Name);
    }

    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        Setup();
        Assert.Null(_registry.Get("nonexistent"));
    }

    [Fact]
    public void Register_DuplicateId_HigherPriorityWins()
    {
        Setup();
        var core = MakeAbility("fireball", "Core Fireball", priority: 0, packName: "core");
        var lf = MakeAbility("fireball", "LF Fireball", priority: 10, packName: "test-pack");
        _registry.Register(core);
        _registry.Register(lf);
        var result = _registry.Get("fireball");
        Assert.NotNull(result);
        Assert.Equal("LF Fireball", result!.Name);
        Assert.Equal("test-pack", result.PackName);
    }

    [Fact]
    public void Register_DuplicateId_LowerPriorityDoesNotOverride()
    {
        Setup();
        var lf = MakeAbility("fireball", "LF Fireball", priority: 10, packName: "test-pack");
        var core = MakeAbility("fireball", "Core Fireball", priority: 0, packName: "core");
        _registry.Register(lf);
        _registry.Register(core);
        var result = _registry.Get("fireball");
        Assert.NotNull(result);
        Assert.Equal("LF Fireball", result!.Name);
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        Setup();
        _registry.Register(MakeAbility("kick"));
        _registry.Register(MakeAbility("fireball"));
        _registry.Register(MakeAbility("dodge"));
        var all = _registry.GetAll().ToList();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void GetByType_FiltersCorrectly()
    {
        Setup();
        _registry.Register(MakeAbility("kick", type: AbilityType.Active));
        _registry.Register(MakeAbility("dodge", type: AbilityType.Passive));
        _registry.Register(MakeAbility("fireball", type: AbilityType.Active));
        var actives = _registry.GetByType(AbilityType.Active).ToList();
        Assert.Equal(2, actives.Count);
        var passives = _registry.GetByType(AbilityType.Passive).ToList();
        Assert.Single(passives);
    }
}
