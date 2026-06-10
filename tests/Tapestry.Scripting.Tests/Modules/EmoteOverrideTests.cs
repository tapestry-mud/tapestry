using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class EmoteOverrideTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, EmoteRegistry emotes, PackDependencyGraph graph)
        BuildRuntime(Dictionary<string, List<string>>? deps = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<PackDependencyGraph>();
        graph.Build(deps ?? new Dictionary<string, List<string>>());
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<RegistrationPolicy>(),
                provider.GetRequiredService<EmoteRegistry>(), graph);
    }

    private const string Wave =
        "tapestry.emotes.register({ name:'wave', self:'You wave.', room:'{actor} waves.' });";
    private const string WaveB =
        "tapestry.emotes.register({ name:'wave', self:'You flail.', room:'{actor} flails.' });";
    private const string WaveBOverride =
        "tapestry.emotes.register({ name:'wave', self:'You flail.', room:'{actor} flails.', override:true });";

    [Fact]
    public void Register_IsDeferred_UntilSeal()
    {
        var (rt, policy, emotes, _) = BuildRuntime();
        rt.Execute(Wave, "pack-a", "scripts/a.js");
        emotes.Get("wave").Should().BeNull("emotes must commit at the seal, not eagerly");
        policy.Resolve();
        emotes.Get("wave").Should().NotBeNull();
    }

    [Fact]
    public void TwoPacks_SameEmote_NoOverride_BootError_NamingBothPacks()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(Wave, "pack-a", "scripts/a.js");
        rt.Execute(WaveB, "pack-b", "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("wave").And.Contain("override")
            .And.Contain("pack-a").And.Contain("pack-b");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, emotes, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        rt.Execute(Wave, "pack-a", "scripts/a.js");
        rt.Execute(WaveBOverride, "pack-b", "scripts/b.js");
        policy.Resolve();
        emotes.Get("wave")!.SelfMessage.Should().Be("You flail.", "the override's definition must win");
    }

    [Fact]
    public void Override_WithoutEdge_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        rt.Execute(Wave, "pack-a", "scripts/a.js");
        rt.Execute(WaveBOverride, "pack-b", "scripts/b.js");
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }
}
