using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Races;

public class RaceRegistryTests
{
    private RaceRegistry _registry = null!;

    private void Setup()
    {
        _registry = new RaceRegistry();
    }

    private RaceDefinition MakeRace(string id, string name = "Test",
        int priority = 0, string packName = "core")
    {
        return new RaceDefinition
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
        _registry.Register(MakeRace("elf", "Elf"));
        var result = _registry.Get("elf");
        Assert.NotNull(result);
        Assert.Equal("Elf", result!.Name);
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
        _registry.Register(MakeRace("elf", "Core Elf", priority: 0, packName: "core"));
        _registry.Register(MakeRace("elf", "LF Elf", priority: 10, packName: "test-pack"));
        Assert.Equal("LF Elf", _registry.Get("elf")!.Name);
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        Setup();
        _registry.Register(MakeRace("human"));
        _registry.Register(MakeRace("folk"));
        _registry.Register(MakeRace("elf"));
        Assert.Equal(3, _registry.GetAll().Count());
    }

    [Fact]
    public void Has_ReturnsTrueForRegistered()
    {
        Setup();
        _registry.Register(MakeRace("elf"));
        Assert.True(_registry.Has("elf"));
        Assert.False(_registry.Has("nope"));
    }
}
