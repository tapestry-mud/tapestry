using FluentAssertions;

namespace Tapestry.Scripting.Tests;

public class ScriptOwnerTests
{
    [Fact]
    public void CurrentPackOwner_PrefersActiveModule_OverStaleGlobal()
    {
        var (runtime, ctx) = JintRuntimeTests.CreateRuntime();
        // Simulate a prior legacy Execute that left the global set:
        runtime.Execute("/* legacy */", "tapestry-core");
        var dir = ctx.RegisterTempPack("test-pack");
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "dist", "scripts", "ping.js"),
            "import { commands } from \"@tapestry/engine\";\n" +
            "commands.register({ name: 'ping', priority: 0, handler: function(p, a) {} });\n");

        runtime.ImportModule(runtime.ModuleKey("test-pack", "dist/scripts/ping.js"));
        ctx.RegistrationPolicy.Resolve();

        // owner is the ESM pack (active module), not the stale "tapestry-core" global
        ctx.CommandRegistry.Resolve("ping")!.PackName.Should().Be("test-pack");
    }

    [Fact]
    public void CurrentPackOwner_LegacyExecute_FallsBackToGlobal()
    {
        var (runtime, ctx) = JintRuntimeTests.CreateRuntime();
        runtime.Execute(
            "tapestry.commands.register({ name: 'legacycmd', priority: 0, handler: function(p,a){} });",
            "tapestry-core");
        ctx.RegistrationPolicy.Resolve();

        ctx.CommandRegistry.Resolve("legacycmd")!.PackName.Should().Be("tapestry-core");
    }
}
