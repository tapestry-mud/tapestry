using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Help;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests.Modules;

public class CommandsModuleTests : IDisposable
{
    private readonly List<string> _temps = new();

    public void Dispose()
    {
        foreach (var t in _temps)
        {
            try { Directory.Delete(t, recursive: true); } catch { }
        }
    }

    private (JintRuntime rt, CommandRegistry registry, World world, RegistrationPolicy policy, HelpService help) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<CommandRegistry>(), provider.GetRequiredService<World>(),
                provider.GetRequiredService<RegistrationPolicy>(), provider.GetRequiredService<HelpService>());
    }

    private string MakeTopicDir(string topicId, string category = "world")
    {
        var root = Path.Combine(Path.GetTempPath(), "cmd-help-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "help"));
        File.WriteAllText(
            Path.Combine(root, "help", topicId + ".yaml"),
            $"id: {topicId}\ntitle: {topicId}\ncategory: {category}\nbody: |\n  Test topic.\n");
        _temps.Add(root);
        return root;
    }

    [Fact]
    public void Register_SourceFileFlowsFromCurrentSource()
    {
        var (rt, registry, _, policy, _) = BuildRuntime();
        rt.Execute(
            "tapestry.commands.register({ name: 'go', handler: function(p,a){} });",
            "test-pack",
            "scripts/commands/movement.js"
        );
        policy.Resolve();
        var reg = registry.Resolve("go");
        Assert.Equal("scripts/commands/movement.js", reg!.SourceFile);
    }

    [Fact]
    public void Register_AdminShorthand_SetsVisibleToAdminPredicate()
    {
        var (rt, registry, _, policy, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.commands.register({
                name: 'spawn',
                admin: true,
                handler: function(player, args) {}
            });
        ");
        policy.Resolve();
        var reg = registry.Resolve("spawn");
        Assert.NotNull(reg!.VisibleTo);

        var adminEntity = new Entity("player", "Admin");
        adminEntity.AddRole("admin");
        Assert.True(reg.VisibleTo!(adminEntity));

        var normalEntity = new Entity("player", "Wanderer");
        Assert.False(reg.VisibleTo!(normalEntity));
    }

    [Fact]
    public void Register_AdminTrue_WinsOverVisibleTo()
    {
        var (rt, registry, _, policy, _) = BuildRuntime();
        rt.Execute(@"
            tapestry.commands.register({
                name: 'secret',
                admin: true,
                visibleTo: function(player) { return true; },
                handler: function(player, args) {}
            });
        ");
        policy.Resolve();
        var reg = registry.Resolve("secret");
        var normalEntity = new Entity("player", "Wanderer");
        Assert.False(reg!.VisibleTo!(normalEntity));
    }

    [Fact]
    public void PlayerObject_HasRole_ReturnsTrueWhenRolePresent()
    {
        var (rt, registry, world, policy, _) = BuildRuntime();
        var entity = new Entity("player", "Tester");
        entity.AddRole("admin");
        world.TrackEntity(entity);

        rt.Execute($@"
            tapestry.commands.register({{
                name: 'tagtest',
                visibleTo: function(player) {{
                    return player.hasRole('admin');
                }},
                handler: function(player, args) {{}}
            }});
        ");
        policy.Resolve();
        var reg = registry.Resolve("tagtest");
        Assert.NotNull(reg!.VisibleTo);
        Assert.True(reg.VisibleTo!(entity));
    }

    [Fact]
    public void ListForPlayer_ExcludesHiddenCommands()
    {
        var (rt, registry, world, policy, help) = BuildRuntime();
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);

        rt.Execute(@"
            tapestry.commands.register({ name: 'visible', handler: function(){} });
            tapestry.commands.register({ name: 'hidden', admin: true, handler: function(){} });
        ");
        // load a help topic for 'visible' so IsListed returns true; 'hidden' is excluded by VisibleTo
        help.LoadPack("test-pack", MakeTopicDir("visible", "world"), "help/**/*.yaml", 1);
        policy.Resolve();

        var result = rt.Evaluate($"JSON.stringify(tapestry.commands.listForPlayer('{player.Id}'))");
        var json = result?.ToString() ?? "[]";

        Assert.Contains("visible", json);
        Assert.DoesNotContain("\"keyword\":\"hidden\"", json);
    }

    [Fact]
    public void ListForPlayer_ReadsCategoryFromHelpTopic()
    {
        var (rt, registry, world, policy, help) = BuildRuntime();
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);

        rt.Execute(
            "tapestry.commands.register({ name: 'north', handler: function(){} });",
            "test-pack",
            "scripts/commands/movement.js"
        );
        help.LoadPack("test-pack", MakeTopicDir("north", "movement"), "help/**/*.yaml", 1);
        policy.Resolve();

        var result = rt.Evaluate($"JSON.stringify(tapestry.commands.listForPlayer('{player.Id}'))");
        var json = result?.ToString() ?? "[]";

        Assert.Contains("\"category\":\"movement\"", json);
    }

    [Fact]
    public void ListForPlayer_IncludesAdminCommandsForAdminPlayer()
    {
        var (rt, registry, world, policy, help) = BuildRuntime();
        var adminPlayer = new Entity("player", "Admin");
        adminPlayer.AddRole("admin");
        world.TrackEntity(adminPlayer);

        rt.Execute(@"
            tapestry.commands.register({ name: 'spawn', admin: true, handler: function(){} });
        ");
        help.LoadPack("test-pack", MakeTopicDir("spawn", "admin"), "help/**/*.yaml", 1);
        policy.Resolve();

        var result = rt.Evaluate($"JSON.stringify(tapestry.commands.listForPlayer('{adminPlayer.Id}'))");
        var json = result?.ToString() ?? "[]";

        Assert.Contains("spawn", json);
    }
}
