using FluentAssertions;

namespace Tapestry.Scripting.Tests;

public class EsmSealCompatTests
{
    [Fact]
    public void EsmOverride_OfLegacyBase_WithDeclaredEdge_Wins()
    {
        var (runtime, ctx) = JintRuntimeTests.CreateRuntime();
        // legacy core registers `tell` (base)
        runtime.Execute("tapestry.commands.register({ name: 'tell', priority: 0, handler: function(p,a){} });", "tapestry-core");
        // esm viewer overrides `tell` with a declared edge viewer->core
        ctx.DeclareEdge("tapestry-viewer", "tapestry-core");
        var dir = ctx.RegisterTempPack("tapestry-viewer");
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "dist", "scripts", "tell.js"),
            "import { commands } from \"@tapestry/engine\";\n" +
            "commands.register({ name: 'tell', override: true, priority: 0, handler: function(p,a){} });\n");
        runtime.ImportModule(runtime.ModuleKey("tapestry-viewer", "dist/scripts/tell.js"));

        ctx.RegistrationPolicy.Resolve();
        ctx.CommandRegistry.Resolve("tell")!.PackName.Should().Be("tapestry-viewer");
    }

    [Fact]
    public void EsmOverride_WithoutEdge_ThrowsAtSeal()
    {
        var (runtime, ctx) = JintRuntimeTests.CreateRuntime();
        runtime.Execute("tapestry.commands.register({ name: 'tell', priority: 0, handler: function(p,a){} });", "tapestry-core");
        var dir = ctx.RegisterTempPack("tapestry-viewer"); // no edge declared
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "dist", "scripts", "tell.js"),
            "import { commands } from \"@tapestry/engine\";\n" +
            "commands.register({ name: 'tell', override: true, priority: 0, handler: function(p,a){} });\n");
        runtime.ImportModule(runtime.ModuleKey("tapestry-viewer", "dist/scripts/tell.js"));

        var act = () => ctx.RegistrationPolicy.Resolve();
        act.Should().Throw<InvalidOperationException>().WithMessage("*declares no dependency*");
    }

    [Fact]
    public void TwoEsmPacks_SameNamedPrivateHelper_BothLoadCleanly()
    {
        var (runtime, ctx) = JintRuntimeTests.CreateRuntime();
        foreach (var ns in new[] { "pack-a", "pack-b" })
        {
            var dir = ctx.RegisterTempPack(ns);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "dist", "scripts", "x.js"),
                "import { commands } from \"@tapestry/engine\";\n" +
                "function padRight(s, n) { return (s + '          ').slice(0, n); }\n" +
                $"commands.register({{ name: '{ns}cmd', priority: 0, handler: function(p,a){{}} }});\n");
            runtime.ImportModule(runtime.ModuleKey(ns, "dist/scripts/x.js"));
        }
        ctx.RegistrationPolicy.Resolve();

        // Both private padRight definitions coexist; the global realm has none.
        ctx.Evaluate("typeof padRight").ToString().Should().Be("undefined");
        ctx.CommandRegistry.Resolve("pack-acmd").Should().NotBeNull();
        ctx.CommandRegistry.Resolve("pack-bcmd").Should().NotBeNull();
    }
}
