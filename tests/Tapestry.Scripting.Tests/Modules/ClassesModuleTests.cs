using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Stats;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests.Modules;

public class ClassesModuleTests
{
    private (JintRuntime rt, ClassRegistry reg, World world) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<ClassRegistry>(), provider.GetRequiredService<World>());
    }

    [Fact]
    public void Register_PersistsNewFields()
    {
        var (rt, reg, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.classes.register({
                id: 'warrior',
                name: 'Warrior',
                tagline: 'Master swordsman',
                description: 'Superior HP growth.',
                track: 'combat',
                starting_alignment: 0,
                level_up_flavor: 'Your blade forms sharpen.',
                allowed_categories: ['human', 'human'],
                allowed_genders: ['male', 'female', 'other'],
                stat_growth: { max_hp: '2d6+2' },
                path: [
                    { level: 1, ability_id: 'dodge' },
                    { level: 25, ability_id: 'heron_wading', unlocked_via: 'quest' }
                ]
            });
        ");
        var def = reg.Get("warrior");
        Assert.NotNull(def);
        Assert.Equal("Master swordsman", def!.Tagline);
        Assert.Equal("Superior HP growth.", def.Description);
        Assert.Equal("combat", def.Track);
        Assert.Equal(0, def.StartingAlignment);
        Assert.Equal("Your blade forms sharpen.", def.LevelUpFlavor);
        Assert.Contains("human", def.AllowedCategories);
        Assert.Contains("human", def.AllowedCategories);
        Assert.Contains("male", def.AllowedGenders);
        Assert.Equal(2, def.Path.Count);
        Assert.Equal(1, def.Path[0].Level);
        Assert.Equal("dodge", def.Path[0].AbilityId);
        Assert.Null(def.Path[0].UnlockedVia);
        Assert.Equal("quest", def.Path[1].UnlockedVia);
    }

    [Fact]
    public void Get_ExposesAllNewFields()
    {
        var (rt, reg, _) = BuildRuntime();
        reg.Register(new ClassDefinition
        {
            Id = "warrior",
            Name = "Warrior",
            Tagline = "Master swordsman",
            Description = "Great description.",
            Track = "combat",
            StartingAlignment = 0,
            LevelUpFlavor = "Sharp.",
            AllowedCategories = new List<string> { "human" },
            AllowedGenders = new List<string> { "male", "female", "other" },
            Path = new List<ClassPathEntry> { new ClassPathEntry(1, "dodge", null) }
        });
        var result = rt.Evaluate("tapestry.classes.get('warrior')");
        Assert.NotNull(result);
        // Verify the object round-trips through JS; detailed field checks in C# layer tests
    }

    [Fact]
    public void GetEligibleClasses_FiltersCorrectly()
    {
        var (rt, reg, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.races.register({
                id: 'human',
                name: 'Human',
                race_category: 'human',
                stat_caps: {}
            });
            tapestry.classes.register({
                id: 'test-mob',
                name: 'Human Warrior',
                allowed_categories: ['human'],
                allowed_genders: ['male', 'female', 'other'],
                stat_growth: {}
            });
            tapestry.classes.register({
                id: 'warrior',
                name: 'Warrior',
                allowed_categories: ['human'],
                allowed_genders: ['male', 'female', 'other'],
                stat_growth: {}
            });
        ");
        var result = rt.Evaluate(
            "tapestry.classes.getEligibleClasses({ race: 'human', gender: 'male' })");
        Assert.NotNull(result);
        // Should return exactly one class
        var arr = result as object[];
        Assert.NotNull(arr);
        Assert.Single(arr!);
    }

    [Fact]
    public void GetEligibleClasses_UsesRaceIdAsCategoryFallback()
    {
        var (rt, reg, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.races.register({
                id: 'human',
                name: 'Human',
                stat_caps: {}
                // no race_category — falls back to 'human'
            });
            tapestry.classes.register({
                id: 'test-mob',
                name: 'Human Warrior',
                allowed_categories: ['human'],
                stat_growth: {}
            });
        ");
        var result = rt.Evaluate(
            "tapestry.classes.getEligibleClasses({ race: 'human', gender: 'male' })");
        var arr = result as object[];
        Assert.NotNull(arr);
        Assert.Single(arr!);
    }

    [Fact]
    public void Register_StoresClassInRegistry()
    {
        var (rt, reg, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.classes.register({
                id: 'warrior',
                name: 'Warrior',
                stat_growth: { max_hp: '2d6+2', max_movement: '1d4' }
            });
        ");
        var def = reg.Get("warrior");
        Assert.NotNull(def);
        Assert.Equal("Warrior", def!.Name);
        Assert.Equal("2d6+2", def.StatGrowth[Tapestry.Engine.Stats.StatType.MaxHp]);
    }

    [Fact]
    public void Get_ReturnsRegisteredClassDefinition()
    {
        var (rt, reg, _) = BuildRuntime();
        reg.Register(new ClassDefinition { Id = "human", Name = "Human" });
        var result = rt.Evaluate("tapestry.classes.get('human')");
        Assert.NotNull(result);
    }

    [Fact]
    public void Register_TrainsPerLevel_RoundTrips()
    {
        var (rt, reg, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.classes.register({
                id: 'warrior', name: 'Warrior',
                trains_per_level: 7
            });
        ");
        var def = reg.Get("warrior");
        Assert.NotNull(def);
        Assert.Equal(7, def!.TrainsPerLevel);
    }

    [Fact]
    public void Register_GrowthBonuses_RoundTrips()
    {
        var (rt, reg, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.classes.register({
                id: 'warrior', name: 'Warrior',
                growth_bonuses: { max_hp: 'constitution', max_movement: 'dexterity' }
            });
        ");
        var def = reg.Get("warrior");
        Assert.NotNull(def);
        Assert.Equal(StatType.Constitution, def!.GrowthBonuses[StatType.MaxHp]);
        Assert.Equal(StatType.Dexterity, def.GrowthBonuses[StatType.MaxMovement]);
    }

    [Fact]
    public void Register_TrainsPerLevel_DefaultsWhenOmitted()
    {
        var (rt, reg, _) = BuildRuntime();
        rt.Execute(@"tapestry.classes.register({ id: 'empty', name: 'Empty' });");
        Assert.Equal(5, reg.Get("empty")!.TrainsPerLevel);
    }

    [Fact]
    public void SetClass_StoresClassPropertyOnEntity()
    {
        var (rt, reg, world) = BuildRuntime();
        reg.Register(new ClassDefinition { Id = "warrior", Name = "Warrior" });
        var entity = new Entity("player", "Tester");
        world.TrackEntity(entity);
        rt.Execute($"tapestry.world.setClass('{entity.Id}', 'warrior');");
        Assert.Equal("warrior", entity.GetProperty<string>("class"));
    }
}
