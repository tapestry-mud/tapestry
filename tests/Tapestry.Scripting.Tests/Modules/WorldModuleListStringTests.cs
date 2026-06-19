using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

/// <summary>
/// Verifies that JS scripts can write a list_string property via setProperty and read it back
/// correctly with Array.isArray / indexOf -- the critical path for the learn command.
/// </summary>
public class WorldModuleListStringTests
{
    private (JintRuntime rt, World world, PropertyRegistry props) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var props = provider.GetRequiredService<PropertyRegistry>();
        // Register known_recipes as list_string applies_to player -- mirrors tinkers/properties.yml
        props.RegisterPackProperty(
            "tapestry-tinkers",
            "known_recipes",
            "The player's recipe book",
            PropertyValueType.ListString,
            appliesTo: new[] { "player" });
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), props);
    }

    [Fact]
    public void SetProperty_WithJsArray_CanBeReadBackViaArrayIsArray()
    {
        var (rt, world, _) = BuildRuntime();
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        var id = player.Id;

        // Simulate the learn command: get (null -> []), push, setProperty
        EsmTest.Load(rt, "test-pack", $@"
            var raw = tapestry.world.getProperty('{id}', 'known_recipes') || [];
            var known = Array.isArray(raw) ? raw.slice() : [];
            known.push('tapestry-tinkers:level-1-bench');
            tapestry.world.setProperty('{id}', 'known_recipes', known);
        ");

        // Verify JS can read it back with Array.isArray
        var isArray = EsmTest.Eval(rt, $"Array.isArray(tapestry.world.getProperty('{id}', 'known_recipes') || [])");
        Assert.Equal(true, isArray);

        // Verify indexOf works (the check in craft.js and recipe.js)
        var hasRecipe = EsmTest.Eval(rt, $@"(function() {{
                var k = tapestry.world.getProperty('{id}', 'known_recipes') || [];
                var list = Array.isArray(k) ? k : [];
                return list.indexOf('tapestry-tinkers:level-1-bench') >= 0;
            }})()");
        Assert.Equal(true, hasRecipe);
    }

    [Fact]
    public void SetProperty_WithJsArray_CanAddSecondEntry()
    {
        var (rt, world, _) = BuildRuntime();
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        var id = player.Id;

        // Add first recipe
        EsmTest.Load(rt, "test-pack", $@"
            var raw = tapestry.world.getProperty('{id}', 'known_recipes') || [];
            var known = Array.isArray(raw) ? raw.slice() : [];
            known.push('tapestry-tinkers:level-1-bench');
            tapestry.world.setProperty('{id}', 'known_recipes', known);
        ");

        // Add second recipe (simulate second learn call)
        EsmTest.Load(rt, "test-pack", $@"
            var raw2 = tapestry.world.getProperty('{id}', 'known_recipes') || [];
            var known2 = Array.isArray(raw2) ? raw2.slice() : [];
            known2.push('tapestry-tinkers:iron-pickaxe');
            tapestry.world.setProperty('{id}', 'known_recipes', known2);
        ");

        var count = EsmTest.Eval(rt, $@"(function() {{
                var k = tapestry.world.getProperty('{id}', 'known_recipes') || [];
                var list = Array.isArray(k) ? k : [];
                return list.length;
            }})()");
        Assert.Equal(2, Convert.ToInt32(count));
    }

    [Fact]
    public void SetProperty_WithJsArray_DoesNotAddDuplicate_WhenIndexOfChecked()
    {
        var (rt, world, _) = BuildRuntime();
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        var id = player.Id;

        // learn same recipe twice -- the learn command checks indexOf first
        EsmTest.Load(rt, "test-pack", $@"
            var raw = tapestry.world.getProperty('{id}', 'known_recipes') || [];
            var known = Array.isArray(raw) ? raw.slice() : [];
            if (known.indexOf('tapestry-tinkers:level-1-bench') < 0) {{
                known.push('tapestry-tinkers:level-1-bench');
                tapestry.world.setProperty('{id}', 'known_recipes', known);
            }}
        ");
        EsmTest.Load(rt, "test-pack", $@"
            var raw = tapestry.world.getProperty('{id}', 'known_recipes') || [];
            var known = Array.isArray(raw) ? raw.slice() : [];
            if (known.indexOf('tapestry-tinkers:level-1-bench') < 0) {{
                known.push('tapestry-tinkers:level-1-bench');
                tapestry.world.setProperty('{id}', 'known_recipes', known);
            }}
        ");

        var count = EsmTest.Eval(rt, $@"(function() {{
                var k = tapestry.world.getProperty('{id}', 'known_recipes') || [];
                return Array.isArray(k) ? k.length : -1;
            }})()");
        Assert.Equal(1, Convert.ToInt32(count));
    }
}
