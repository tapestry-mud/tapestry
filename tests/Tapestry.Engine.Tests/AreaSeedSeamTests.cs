using Tapestry.Engine;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AreaSeedSeamTests
{
    [Fact]
    public void Seed_persists_through_area_serialization_round_trip()
    {
        var def = new AreaDefinition { Id = "castle-kitchen", Name = "Castle Kitchen", Seed = 8473920113L };
        var yaml = YamlContentLoader.SerializeAreaDefinition(def);
        Assert.Contains("seed", yaml);
        var reloaded = YamlContentLoader.LoadAreaDefinition(yaml);
        Assert.Equal(8473920113L, reloaded.Seed);
    }

    [Fact]
    public void SetAreaAttribute_seed_writes_the_value()
    {
        using var root = new TempAuthoringRoot();
        root.CreateArea("castle-kitchen", "Castle Kitchen");
        root.SetAreaAttribute("castle-kitchen", "seed", "8473920113");
        Assert.Equal(8473920113L, root.GetAreaDefinition("castle-kitchen")!.Seed);
    }
}
