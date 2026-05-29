using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class PackValidatorInteropResolutionTests
{
    private static PackValidator BuildValidator(
        PackDependencyGraph graph,
        PackExportRegistry exports,
        InteropCallSiteRegistry callSites)
    {
        var world = new World();
        var items = new ItemRegistry();
        var spawner = new SpawnManager(world, new EventBus(), new LootTableResolver(), items);
        var tags = new TagRegistry();
        tags.SetDependencyResolver(_ => Enumerable.Empty<string>());
        var props = new PropertyRegistry();
        // No manifests => Check 1 is a no-op; this fixture isolates Check 2.
        var manifests = new StaticManifestProviderForInterop(Array.Empty<PackManifest>());
        return new PackValidator(
            spawner, items, world, NullLogger<PackValidator>.Instance,
            new AbilityRegistry(), new CommandRegistry(), tags, manifests, props,
            graph, exports, callSites);
    }

    // cooking -> survival edge declared; survival is loaded.
    private static PackDependencyGraph EdgeGraph()
    {
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tapestry-cooking"] = new() { "tapestry-survival" },
            ["tapestry-survival"] = new(),
        });
        return graph;
    }

    // A fake export entry — the handler is never invoked during validation, so JsValue.Null is fine.
    private static PackExportRegistry ExportsWith(string pack, string name)
    {
        var exports = new PackExportRegistry();
        exports.Register(new ExportEntry(
            pack, name, Jint.Native.JsValue.Null, "", Array.Empty<string>(), "", "query",
            new[] { "all" }));
        return exports;
    }

    private static InteropCallSiteRegistry Sites(params InteropCallSite[] s)
    {
        var reg = new InteropCallSiteRegistry();
        foreach (var site in s) { reg.Record(site); }
        return reg;
    }

    [Fact]
    public void Validate_CallToNonExistentExport_Throws()
    {
        var validator = BuildValidator(
            EdgeGraph(),
            new PackExportRegistry(), // survival registers nothing
            Sites(new InteropCallSite("tapestry-cooking", "tapestry-survival", "getHungrTier", InteropCallKind.Call, "scripts/cook.js", 14)));

        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate());
        Assert.Contains("getHungrTier", ex.Message);
        Assert.Contains("does not exist", ex.Message);
        Assert.Contains("scripts/cook.js:14", ex.Message);
    }

    [Fact]
    public void Validate_CallWithNoDeclaredEdge_Throws()
    {
        // rogue -> survival has no edge.
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tapestry-rogue"] = new(),
            ["tapestry-survival"] = new(),
        });
        var validator = BuildValidator(
            graph,
            ExportsWith("tapestry-survival", "getHungerTier"),
            Sites(new InteropCallSite("tapestry-rogue", "tapestry-survival", "getHungerTier", InteropCallKind.Call, "f.js", 3)));

        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate());
        Assert.Contains("no dependency", ex.Message);
        Assert.Contains("tapestry-survival", ex.Message);
    }

    [Fact]
    public void Validate_ValidCall_Passes()
    {
        var validator = BuildValidator(
            EdgeGraph(),
            ExportsWith("tapestry-survival", "getHungerTier"),
            Sites(new InteropCallSite("tapestry-cooking", "tapestry-survival", "getHungerTier", InteropCallKind.Call, "f.js", 1)));

        validator.Validate(); // edge declared + export exists => fine
    }

    [Fact]
    public void Validate_SelfCall_Passes()
    {
        // caller == target: exempt from both edge and export checks (matches call-time behavior).
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tapestry-survival"] = new(),
        });
        var validator = BuildValidator(
            graph,
            new PackExportRegistry(), // not even registered — self-call is not checked
            Sites(new InteropCallSite("tapestry-survival", "tapestry-survival", "ping", InteropCallKind.Call, "f.js", 1)));

        validator.Validate(); // does not throw
    }

    [Fact]
    public void Validate_HasSiteWithMissingExport_DoesNotThrow()
    {
        // `has(pack, export)` is a non-throwing existence probe — the whole point is to let a pack
        // detect an absent optional capability and skip it. A `has` site must enforce the edge but
        // NEVER throw on a missing export, matching PacksModule.Has (line 182).
        var validator = BuildValidator(
            EdgeGraph(),
            new PackExportRegistry(), // export deliberately absent
            Sites(new InteropCallSite("tapestry-cooking", "tapestry-survival", "missingExport", InteropCallKind.Has, "f.js", 7)));

        validator.Validate(); // does not throw — has tolerates absence by design
    }

    [Fact]
    public void Validate_HasSiteWithUndeclaredEdge_Throws()
    {
        // Even `has` cannot probe a pack the caller never declared a dependency on — the edge is
        // enforced for both kinds (PacksModule.Has calls EnforceEdge before the existence check).
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tapestry-rogue"] = new(),
            ["tapestry-survival"] = new(),
        });
        var validator = BuildValidator(
            graph,
            ExportsWith("tapestry-survival", "getHungerTier"),
            Sites(new InteropCallSite("tapestry-rogue", "tapestry-survival", "getHungerTier", InteropCallKind.Has, "f.js", 2)));

        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate());
        Assert.Contains("no dependency", ex.Message);
    }

    [Fact]
    public void Validate_GuardedCallToAbsentOptionalDep_Passes()
    {
        // cooking declares an OPTIONAL dep on survival (so DeclaresEdge is true — the graph unions
        // required + optional edges), but survival is NOT loaded, so its exports were never
        // registered. A call site guarded by `has` will never run when the dep is absent. Boot must
        // NOT throw: skip export resolution whenever the target pack isn't loaded.
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tapestry-cooking"] = new() { "tapestry-survival" }, // optional edge present...
            // ...but tapestry-survival is absent from the loaded set
        });
        var validator = BuildValidator(
            graph,
            new PackExportRegistry(), // nothing registered for the absent dep
            Sites(new InteropCallSite("tapestry-cooking", "tapestry-survival", "getHungerTier", InteropCallKind.Call, "f.js", 5)));

        validator.Validate(); // does not throw — target not loaded => export check skipped
    }

    [Fact]
    public void Validate_NoRecordedSites_Passes()
    {
        // Non-literal/dynamic calls are never recorded, so the registry is empty => nothing to check.
        var validator = BuildValidator(EdgeGraph(), new PackExportRegistry(), Sites());

        validator.Validate(); // does not throw
    }
}

file sealed class StaticManifestProviderForInterop : IPackManifestProvider
{
    private readonly IReadOnlyList<PackManifest> _packs;
    public StaticManifestProviderForInterop(IReadOnlyList<PackManifest> packs) { _packs = packs; }
    public IReadOnlyList<PackManifest> LoadedPacks => _packs;
}
