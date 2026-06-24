using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AreaSeedSeamTests
{
    [Fact]
    public void AreaModule_get_projects_theme_and_seed()
    {
        var registry = new AreaRegistry();
        registry.Register(new AreaDefinition
        {
            Id = "sunken-ship",
            Name = "The Sunken Ship",
            Theme = "a sunken cargo ship",
            Seed = 2000706016L,
        });
        var tick = new AreaTickService(new World(), new EventBus(), registry, new ServerConfig());
        var module = new AreaModule(tick, registry);

        var host = module.Build(null!);
        var getFn = (System.Func<string, object?>)host.GetType().GetProperty("get")!.GetValue(host)!;
        var result = getFn("sunken-ship")!;

        var theme = result.GetType().GetProperty("theme")!.GetValue(result);
        var seed = result.GetType().GetProperty("seed")!.GetValue(result);
        Assert.Equal("a sunken cargo ship", theme);
        Assert.Equal(2000706016L, seed);
    }

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
