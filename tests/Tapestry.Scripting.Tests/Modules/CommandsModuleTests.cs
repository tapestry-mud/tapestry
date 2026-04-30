using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests.Modules;

public class CommandsModuleTests
{
    private (JintRuntime rt, CommandRegistry registry, World world) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<CommandRegistry>(), provider.GetRequiredService<World>());
    }

    [Fact]
    public void Register_CapturesDescriptionField()
    {
        var (rt, registry, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.commands.register({
                name: 'look',
                description: 'Look at your surroundings.',
                handler: function(player, args) {}
            });
        ");
        var reg = registry.Resolve("look");
        Assert.Equal("Look at your surroundings.", reg!.Description);
    }

    [Fact]
    public void Register_CapturesCategoryField()
    {
        var (rt, registry, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.commands.register({
                name: 'north',
                category: 'movement',
                handler: function(player, args) {}
            });
        ");
        var reg = registry.Resolve("north");
        Assert.Equal("movement", reg!.Category);
    }

    [Fact]
    public void Register_SourceFileFlowsFromCurrentSource()
    {
        var (rt, registry, _) = BuildRuntime();
        rt.Execute(
            "tapestry.commands.register({ name: 'go', handler: function(p,a){} });",
            "test-pack",
            "scripts/commands/movement.js"
        );
        var reg = registry.Resolve("go");
        Assert.Equal("scripts/commands/movement.js", reg!.SourceFile);
    }

    [Fact]
    public void Register_AdminShorthand_SetsVisibleToAdminPredicate()
    {
        var (rt, registry, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.commands.register({
                name: 'spawn',
                admin: true,
                handler: function(player, args) {}
            });
        ");
        var reg = registry.Resolve("spawn");
        Assert.NotNull(reg!.VisibleTo);

        var adminEntity = new Entity("player", "Admin");
        adminEntity.AddTag("admin");
        Assert.True(reg.VisibleTo!(adminEntity));

        var normalEntity = new Entity("player", "Wanderer");
        Assert.False(reg.VisibleTo!(normalEntity));
    }

    [Fact]
    public void Register_AdminTrue_WinsOverVisibleTo()
    {
        var (rt, registry, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.commands.register({
                name: 'secret',
                admin: true,
                visibleTo: function(player) { return true; },
                handler: function(player, args) {}
            });
        ");
        var reg = registry.Resolve("secret");
        var normalEntity = new Entity("player", "Wanderer");
        Assert.False(reg!.VisibleTo!(normalEntity));
    }

    [Fact]
    public void PlayerObject_HasTag_ReturnsTrueWhenTagPresent()
    {
        var (rt, registry, world) = BuildRuntime();
        var entity = new Entity("player", "Tester");
        entity.AddTag("admin");
        world.TrackEntity(entity);

        rt.Execute($@"
            tapestry.commands.register({{
                name: 'tagtest',
                visibleTo: function(player) {{
                    return player.hasTag('admin');
                }},
                handler: function(player, args) {{}}
            }});
        ");
        var reg = registry.Resolve("tagtest");
        Assert.NotNull(reg!.VisibleTo);
        Assert.True(reg.VisibleTo!(entity));
    }

    [Fact]
    public void ListForPlayer_ExcludesHiddenCommands()
    {
        var (rt, registry, world) = BuildRuntime();
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);

        rt.Execute(@"
            tapestry.commands.register({ name: 'visible', description: 'yes', handler: function(){} });
            tapestry.commands.register({ name: 'hidden', admin: true, handler: function(){} });
        ");

        var result = rt.Evaluate($"JSON.stringify(tapestry.commands.listForPlayer('{player.Id}'))");
        var json = result?.ToString() ?? "[]";

        Assert.Contains("visible", json);
        Assert.DoesNotContain("hidden", json);
    }

    [Fact]
    public void ListForPlayer_DerivesCategory_FromFileStem()
    {
        var (rt, registry, world) = BuildRuntime();
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);

        rt.Execute(
            "tapestry.commands.register({ name: 'north', handler: function(){} });",
            "test-pack",
            "scripts/commands/movement.js"
        );

        var result = rt.Evaluate($"JSON.stringify(tapestry.commands.listForPlayer('{player.Id}'))");
        var json = result?.ToString() ?? "[]";

        Assert.Contains("\"category\":\"movement\"", json);
    }

    [Fact]
    public void ListForPlayer_IncludesAdminCommandsForAdminPlayer()
    {
        var (rt, registry, world) = BuildRuntime();
        var adminPlayer = new Entity("player", "Admin");
        adminPlayer.AddTag("admin");
        world.TrackEntity(adminPlayer);

        rt.Execute(@"
            tapestry.commands.register({ name: 'spawn', admin: true, handler: function(){} });
        ");

        var result = rt.Evaluate($"JSON.stringify(tapestry.commands.listForPlayer('{adminPlayer.Id}'))");
        var json = result?.ToString() ?? "[]";

        Assert.Contains("spawn", json);
    }
}
