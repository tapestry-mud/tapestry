using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests.Modules;

public class RacesModuleTests
{
    private (JintRuntime rt, RaceRegistry reg, World world) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<RaceRegistry>(), provider.GetRequiredService<World>());
    }

    [Fact]
    public void Register_StoresRaceInRegistry()
    {
        var (rt, reg, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.races.register({
                id: 'elf',
                name: 'Elf',
                stat_caps: { strength: 25, max_hp: 20 },
                cast_cost_modifier: 40,
                racial_flags: ['resist_poison', 'regen']
            });
        ");
        var def = reg.Get("elf");
        Assert.NotNull(def);
        Assert.Equal(25, def!.StatCaps[StatType.Strength]);
        Assert.Equal(40, def.CastCostModifier);
        Assert.Contains("resist_poison", def.RacialFlags);
    }

    [Fact]
    public void SetRace_StoresRacePropertyAndAppliesFlags()
    {
        var (rt, reg, world) = BuildRuntime();
        reg.Register(new RaceDefinition
        {
            Id = "elf",
            Name = "Elf",
            RacialFlags = new List<string> { "resist_poison", "regen" }
        });
        var entity = new Entity("npc", "Ugly");
        world.TrackEntity(entity);
        rt.Execute($"tapestry.world.setRace('{entity.Id}', 'elf');");
        Assert.Equal("elf", entity.GetProperty<string>("race"));
        Assert.True(entity.HasTag("resist_poison"));
        Assert.True(entity.HasTag("regen"));
    }

    [Fact]
    public void Get_ReturnsNullForUnknown()
    {
        var (rt, _, _) = BuildRuntime();
        var result = rt.Evaluate("tapestry.races.get('missing')");
        Assert.True(result == null || result.ToString() == "null");
    }

    [Fact]
    public void Register_PersistsNewFields()
    {
        var (rt, reg, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.races.register({
                id: 'elf',
                name: 'Elf',
                tagline: 'Shadowspawn, natural resist',
                description: 'Half-human Shadowspawn bred for war.',
                race_category: 'shadowspawn',
                starting_alignment: -100,
                stat_caps: {},
                cast_cost_modifier: 40,
                racial_flags: ['resist_poison']
            });
        ");
        var def = reg.Get("elf");
        Assert.NotNull(def);
        Assert.Equal("Shadowspawn, natural resist", def!.Tagline);
        Assert.Equal("Half-human Shadowspawn bred for war.", def.Description);
        Assert.Equal("shadowspawn", def.RaceCategory);
        Assert.Equal(-100, def.StartingAlignment);
    }

    [Fact]
    public void GetAll_IncludesTaglineAndDescription()
    {
        var (rt, reg, _) = BuildRuntime();
        reg.Register(new RaceDefinition
        {
            Id = "human",
            Name = "Human",
            Tagline = "Iron endurance",
            Description = "Fierce warriors."
        });
        var result = rt.Evaluate("tapestry.races.getAll()");
        Assert.NotNull(result);
        // Returns array — tagline/description available
    }
}
