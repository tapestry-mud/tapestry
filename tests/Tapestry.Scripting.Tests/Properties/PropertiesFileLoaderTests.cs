using Tapestry.Engine.Persistence;
using Tapestry.Scripting.Properties;

namespace Tapestry.Scripting.Tests.Properties;

public class PropertiesFileLoaderTests
{
    private const string SimpleYaml = """
        properties:
          loyalty_score:
            description: "Faction loyalty for quest gating"
            type: int
            applies_to: [player]
          shop_greeting:
            description: "Custom greeting when player enters shop"
            type: string
            applies_to: [npc]
        """;

    [Fact]
    public void LoadIntoRegistry_RegistersPackProperties()
    {
        var registry = new PropertyRegistry();
        PropertiesFileLoader.LoadFromYaml("my-pack", SimpleYaml, registry);
        Assert.True(registry.IsKnown("loyalty_score", "my-pack"));
        Assert.True(registry.IsKnown("shop_greeting", "my-pack"));
    }

    [Fact]
    public void LoadIntoRegistry_SetsCorrectValueType()
    {
        var registry = new PropertyRegistry();
        PropertiesFileLoader.LoadFromYaml("my-pack", SimpleYaml, registry);
        registry.TryResolve("my-pack:loyalty_score", null, out var entry);
        Assert.Equal(PropertyValueType.Int, entry.ValueType);
    }

    [Fact]
    public void LoadIntoRegistry_SetsAppliesTo()
    {
        var registry = new PropertyRegistry();
        PropertiesFileLoader.LoadFromYaml("my-pack", SimpleYaml, registry);
        registry.TryResolve("my-pack:loyalty_score", null, out var entry);
        Assert.NotNull(entry.AppliesTo);
        Assert.True(entry.AppliesToType("player"));
        Assert.False(entry.AppliesToType("npc"));
    }

    [Fact]
    public void LoadIntoRegistry_NoAppliesTo_MatchesAll()
    {
        var yaml = """
            properties:
              score:
                description: "A score"
                type: int
            """;
        var registry = new PropertyRegistry();
        PropertiesFileLoader.LoadFromYaml("my-pack", yaml, registry);
        registry.TryResolve("my-pack:score", null, out var entry);
        Assert.Null(entry.AppliesTo);
    }

    [Fact]
    public void LoadIntoRegistry_MapIntType_Registered()
    {
        var yaml = """
            properties:
              reputation:
                description: "Per-faction reputation"
                type: map_int
                applies_to: [player]
            """;
        var registry = new PropertyRegistry();
        PropertiesFileLoader.LoadFromYaml("my-pack", yaml, registry);
        registry.TryResolve("my-pack:reputation", null, out var entry);
        Assert.Equal(PropertyValueType.MapInt, entry.ValueType);
    }

    [Fact]
    public void LoadIntoRegistry_EmptyFile_DoesNotThrow()
    {
        var registry = new PropertyRegistry();
        PropertiesFileLoader.LoadFromYaml("my-pack", "properties: {}", registry);
    }

    [Fact]
    public void LoadFromFile_FileNotFound_DoesNotThrow()
    {
        var registry = new PropertyRegistry();
        PropertiesFileLoader.LoadIntoRegistry("/nonexistent/path", "my-pack", registry);
    }
}
