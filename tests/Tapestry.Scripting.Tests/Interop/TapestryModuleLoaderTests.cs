using System.IO;
using FluentAssertions;
using Jint;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Tests.Interop;

public class TapestryModuleLoaderTests
{
    private static (JintEngine engine, TapestryModuleLoader loader, FakeEdges edges, string root) NewEngine()
    {
        var root = Directory.CreateTempSubdirectory("tap-esm-").FullName;
        var edges = new FakeEdges();
        var loader = new TapestryModuleLoader(edges);
        var engine = new JintEngine(o => o.EnableModules(loader));
        engine.Modules.Add("@tapestry/engine", b => b.ExportFunction("noop", _ => Jint.Native.JsValue.Undefined));
        return (engine, loader, edges, root);
    }

    [Fact]
    public void RelativeImport_WithinPack_IsAllowed()
    {
        var (engine, loader, _, root) = NewEngine();
        var dir = Path.Combine(root, "survival");
        Directory.CreateDirectory(Path.Combine(dir, "dist", "scripts"));
        File.WriteAllText(Path.Combine(dir, "dist", "scripts", "a.js"),
            "import { v } from \"./b.js\";\nexport const out = v;\n");
        File.WriteAllText(Path.Combine(dir, "dist", "scripts", "b.js"), "export const v = 7;\n");
        loader.Build(new Dictionary<string, string> { ["tapestry-survival"] = dir },
            new HashSet<(string, string)>());

        var ns = engine.Modules.Import(loader.ModuleKey("tapestry-survival", "dist/scripts/a.js"));
        ns.Get("out").AsNumber().Should().Be(7);
    }

    [Fact]
    public void CrossPackImport_WithoutDeclaredEdge_ThrowsLocatedError()
    {
        var (engine, loader, _, root) = NewEngine();
        var cook = Path.Combine(root, "cooking");
        var surv = Path.Combine(root, "survival");
        Directory.CreateDirectory(Path.Combine(cook, "dist", "scripts"));
        Directory.CreateDirectory(Path.Combine(surv, "dist", "scripts"));
        File.WriteAllText(Path.Combine(cook, "dist", "scripts", "c.js"),
            "import { x } from \"@tapestry/survival\";\nexport const out = x;\n");
        File.WriteAllText(Path.Combine(surv, "dist", "scripts", "index.js"), "export const x = 1;\n");
        loader.Build(new Dictionary<string, string>
        {
            ["tapestry-cooking"] = cook,
            ["tapestry-survival"] = surv,
        }, new HashSet<(string, string)>()); // no edge declared

        var act = () => engine.Modules.Import(loader.ModuleKey("tapestry-cooking", "dist/scripts/c.js"));
        act.Should().Throw<Exception>().WithMessage("*no declared dependency*tapestry-survival*");
    }

    [Fact]
    public void OptionalCrossPackImport_TargetAbsent_ResolvesToEmptyModule()
    {
        var (engine, loader, edges, root) = NewEngine();
        var cook = Path.Combine(root, "cooking");
        Directory.CreateDirectory(Path.Combine(cook, "dist", "scripts"));
        File.WriteAllText(Path.Combine(cook, "dist", "scripts", "c.js"),
            "import * as survival from \"@tapestry/survival\";\n" +
            "export const has = (typeof survival.applyWellFedBuff === \"function\");\n");
        edges.Edges.Add(("tapestry-cooking", "tapestry-survival"));
        // survival NOT in packDirs (absent), but the edge is optional:
        loader.Build(new Dictionary<string, string> { ["tapestry-cooking"] = cook },
            new HashSet<(string, string)> { ("tapestry-cooking", "tapestry-survival") });

        var ns = engine.Modules.Import(loader.ModuleKey("tapestry-cooking", "dist/scripts/c.js"));
        ns.Get("has").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void PackOf_And_SourceOf_ParseLocation()
    {
        TapestryModuleLoader.PackOf("pack:tapestry-survival::dist/scripts/a.js")
            .Should().Be("tapestry-survival");
        TapestryModuleLoader.SourceOf("pack:tapestry-survival::dist/scripts/a.js")
            .Should().Be("dist/scripts/a.js");
        TapestryModuleLoader.PackOf("@tapestry/engine").Should().BeNull();
    }

    // ESM supports cross-pack cycles via live bindings; prove it resolves. Two packs with
    // mutual declared edges import each other's entry and use the binding only inside a
    // function (deferred), so live bindings resolve cleanly.
    [Fact]
    public void CircularCrossPackImport_WithMutualEdges_ResolvesViaLiveBindings()
    {
        var (engine, loader, edges, root) = NewEngine();
        var ax = Path.Combine(root, "a"); var bx = Path.Combine(root, "b");
        Directory.CreateDirectory(Path.Combine(ax, "dist", "scripts"));
        Directory.CreateDirectory(Path.Combine(bx, "dist", "scripts"));
        File.WriteAllText(Path.Combine(ax, "dist", "scripts", "index.js"),
            "import { fromB } from \"@tapestry/b\";\nexport const fromA = 'a';\nexport function useB() { return fromB; }\n");
        File.WriteAllText(Path.Combine(bx, "dist", "scripts", "index.js"),
            "import { fromA } from \"@tapestry/a\";\nexport const fromB = 'b';\nexport function useA() { return fromA; }\n");
        edges.Edges.Add(("tapestry-a", "tapestry-b"));
        edges.Edges.Add(("tapestry-b", "tapestry-a"));
        loader.Build(new Dictionary<string, string> { ["tapestry-a"] = ax, ["tapestry-b"] = bx },
            new HashSet<(string, string)>());

        var ns = engine.Modules.Import(loader.ModuleKey("tapestry-a", "dist/scripts/index.js"));
        engine.Invoke(ns.Get("useB")).AsString().Should().Be("b");
    }

    [Fact]
    public void TopLevelImport_ByCanonicalKey_LoadsTheFile()
    {
        var (engine, loader, _, root) = NewEngine();
        var dir = Path.Combine(root, "survival");
        Directory.CreateDirectory(Path.Combine(dir, "dist", "scripts"));
        File.WriteAllText(Path.Combine(dir, "dist", "scripts", "init.js"), "export const ok = true;\n");
        loader.Build(new Dictionary<string, string> { ["tapestry-survival"] = dir },
            new HashSet<(string, string)>());

        var ns = engine.Modules.Import(loader.ModuleKey("tapestry-survival", "dist/scripts/init.js"));
        ns.Get("ok").AsBoolean().Should().BeTrue();
    }
}
