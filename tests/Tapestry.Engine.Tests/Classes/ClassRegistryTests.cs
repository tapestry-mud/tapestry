using Tapestry.Engine.Classes;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Classes;

public class ClassRegistryTests
{
    private ClassRegistry _registry = null!;

    private void Setup()
    {
        _registry = new ClassRegistry();
    }

    private ClassDefinition MakeClass(string id, string name = "Test",
        int priority = 0, string packName = "core")
    {
        return new ClassDefinition
        {
            Id = id,
            Name = name,
            Priority = priority,
            PackName = packName
        };
    }

    [Fact]
    public void Register_And_Get_ReturnsDefinition()
    {
        Setup();
        var cls = MakeClass("warrior", "Warrior");
        _registry.Register(cls);
        var result = _registry.Get("warrior");
        Assert.NotNull(result);
        Assert.Equal("Warrior", result!.Name);
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
        var core = MakeClass("warrior", "Core Warrior", priority: 0, packName: "core");
        var lf = MakeClass("warrior", "LF Warrior", priority: 10, packName: "test-pack");
        _registry.Register(core);
        _registry.Register(lf);
        var result = _registry.Get("warrior");
        Assert.NotNull(result);
        Assert.Equal("LF Warrior", result!.Name);
    }

    [Fact]
    public void Register_DuplicateId_LowerPriorityDoesNotOverride()
    {
        Setup();
        _registry.Register(MakeClass("warrior", "LF Warrior", priority: 10, packName: "test-pack"));
        _registry.Register(MakeClass("warrior", "Core Warrior", priority: 0, packName: "core"));
        var result = _registry.Get("warrior");
        Assert.Equal("LF Warrior", result!.Name);
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        Setup();
        _registry.Register(MakeClass("warrior"));
        _registry.Register(MakeClass("human"));
        _registry.Register(MakeClass("mage"));
        Assert.Equal(3, _registry.GetAll().Count());
    }

    [Fact]
    public void Has_ReturnsTrueForRegistered()
    {
        Setup();
        _registry.Register(MakeClass("warrior"));
        Assert.True(_registry.Has("warrior"));
        Assert.False(_registry.Has("missing"));
    }

    [Fact]
    public void GetEligibleClasses_EmptyAllowedCategories_ReturnsClass()
    {
        Setup();
        _registry.Register(new ClassDefinition { Id = "warrior", Name = "Warrior" });
        var result = _registry.GetEligibleClasses("human", "male").ToList();
        Assert.Single(result);
    }

    [Fact]
    public void GetEligibleClasses_FiltersByRaceCategory()
    {
        Setup();
        _registry.Register(new ClassDefinition
        {
            Id = "test-mob",
            Name = "Elf Ranger",
            AllowedCategories = new List<string> { "elf" }
        });
        _registry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            AllowedCategories = new List<string> { "human" }
        });
        var result = _registry.GetEligibleClasses("elf", "male").ToList();
        Assert.Single(result);
        Assert.Equal("test-mob", result[0].Id);
    }

    [Fact]
    public void GetEligibleClasses_SingletonFallback_UsesRaceIdWhenCategoryEmpty()
    {
        Setup();
        _registry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            AllowedCategories = new List<string> { "human" }
        });
        // Pass raceId directly as category (caller resolves this when RaceCategory is empty)
        var result = _registry.GetEligibleClasses("human", "male").ToList();
        Assert.Single(result);
    }

    [Fact]
    public void GetEligibleClasses_EmptyAllowedGenders_ReturnsClass()
    {
        Setup();
        _registry.Register(new ClassDefinition { Id = "warrior", Name = "Warrior" });
        var result = _registry.GetEligibleClasses("human", "female").ToList();
        Assert.Single(result);
    }

    [Fact]
    public void GetEligibleClasses_FiltersByGender()
    {
        Setup();
        _registry.Register(new ClassDefinition
        {
            Id = "aes_sedai",
            Name = "Aes Sedai",
            AllowedGenders = new List<string> { "female" }
        });
        _registry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            AllowedGenders = new List<string> { "male", "female", "other" }
        });
        var result = _registry.GetEligibleClasses("human", "male").ToList();
        Assert.Single(result);
        Assert.Equal("warrior", result[0].Id);
    }

    [Fact]
    public void GetEligibleClasses_BothFiltersApply()
    {
        Setup();
        _registry.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            AllowedCategories = new List<string> { "human" },
            AllowedGenders = new List<string> { "male", "female", "other" }
        });
        _registry.Register(new ClassDefinition
        {
            Id = "mage",
            Name = "Mage",
            AllowedCategories = new List<string> { "shadowspawn" },
            AllowedGenders = new List<string> { "male", "female", "other" }
        });
        var result = _registry.GetEligibleClasses("human", "male").ToList();
        Assert.Single(result);
        Assert.Equal("warrior", result[0].Id);
    }
}
