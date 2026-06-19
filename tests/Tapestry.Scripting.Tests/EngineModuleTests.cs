using FluentAssertions;

namespace Tapestry.Scripting.Tests;

public class EngineModuleTests
{
    [Fact]
    public void EsmModule_ImportsEngineApi_RegistersCommand()
    {
        var (runtime, ctx) = JintRuntimeTests.CreateRuntime();
        var dir = ctx.RegisterTempPack("test-pack");
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "dist", "scripts", "ping.js"),
            "import { commands } from \"@tapestry/engine\";\n" +
            "commands.register({ name: 'ping', priority: 0, handler: function(p, a) {} });\n");

        runtime.ImportModule(runtime.ModuleKey("test-pack", "dist/scripts/ping.js"));
        ctx.RegistrationPolicy.Resolve();

        ctx.CommandRegistry.Resolve("ping").Should().NotBeNull();
    }

    [Fact]
    public void GetActivePack_OutsideAnyModule_ReturnsNull()
    {
        var (runtime, _) = JintRuntimeTests.CreateRuntime();
        runtime.GetActivePack().Should().BeNull();
    }
}
