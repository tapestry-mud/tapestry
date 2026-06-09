using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Help;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class HelpOverrideTests : IDisposable
{
    private readonly List<string> _temps = new();

    private string MakePackHelp(string topicId, string body = "x", bool over = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "help-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "help"));
        var ov = over ? "override: true\n" : "";
        File.WriteAllText(
            Path.Combine(root, "help", topicId + ".yaml"),
            $"id: {topicId}\ntitle: {topicId}\ncategory: general\n{ov}body: |\n  {body}\n");
        _temps.Add(root);
        return root;
    }

    private static (RegistrationPolicy policy, HelpService help, PackDependencyGraph graph)
        Build(Dictionary<string, List<string>>? deps = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<PackDependencyGraph>();
        graph.Build(deps ?? new Dictionary<string, List<string>>());
        return (provider.GetRequiredService<RegistrationPolicy>(),
                provider.GetRequiredService<HelpService>(), graph);
    }

    [Fact]
    public void TwoPacks_SameTopicId_NoOverride_BootError()
    {
        var (policy, help, _) = Build();
        help.LoadPack("pack-a", MakePackHelp("combat"), "help/**/*.yaml", 10);
        help.LoadPack("pack-b", MakePackHelp("combat"), "help/**/*.yaml", 20);

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("combat").And.Contain("override");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (policy, help, _) = Build(new() { ["pack-b"] = new() { "pack-a" } });
        help.LoadPack("pack-a", MakePackHelp("combat", "base"), "help/**/*.yaml", 10);
        help.LoadPack("pack-b", MakePackHelp("combat", "override", over: true), "help/**/*.yaml", 20);

        policy.Resolve();
        help.Query(null, "combat").Topic!.PackName.Should().Be("pack-b");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (policy, help, _) = Build(); // no edges
        help.LoadPack("pack-a", MakePackHelp("combat"), "help/**/*.yaml", 10);
        help.LoadPack("pack-b", MakePackHelp("combat", over: true), "help/**/*.yaml", 20);
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void SingleTopic_RegistersNormally_AfterResolve()
    {
        var (policy, help, _) = Build();
        help.LoadPack("pack-a", MakePackHelp("races"), "help/**/*.yaml", 10);
        policy.Resolve();
        help.Query(null, "races").Status.Should().Be("ok");
    }

    [Fact]
    public void AutoGen_RunsAfterSeal_FillsCommandGap()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PackDependencyGraph>().Build(new Dictionary<string, List<string>>());
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        var policy = provider.GetRequiredService<RegistrationPolicy>();
        var help = provider.GetRequiredService<HelpService>();
        var commands = provider.GetRequiredService<CommandRegistry>();
        var edges = provider.GetRequiredService<Tapestry.Engine.Registration.IPackEdgeOracle>();

        rt.Execute(
            "tapestry.commands.register({ name:'frob', description:'Frobs.', " +
            "args:{ target:{ type:'npc', required:true } }, handler:function(a,r){} });",
            "tapestry-core", "scripts/frob.js");

        // Before the seal: command not committed, no help.
        help.Query(null, "frob").Status.Should().Be("no_match");

        policy.Resolve();
        new HelpSeal(help, commands, edges).Seal();

        // After seal + HelpSeal: auto-gen filled the gap.
        var topic = help.Query(null, "frob").Topic!;
        topic.PackName.Should().Be("tapestry-core");
    }

    public void Dispose()
    {
        foreach (var t in _temps)
        {
            try { Directory.Delete(t, recursive: true); } catch { /* best effort */ }
        }
    }
}
