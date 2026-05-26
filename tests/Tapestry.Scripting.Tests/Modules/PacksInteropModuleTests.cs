using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Jint.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class PacksInteropModuleTests
{
    private static (JintRuntime rt, PackExportRegistry exports) CreateRuntime()
    {
        var exports = new PackExportRegistry();
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>
        {
            ["tapestry-cooking"] = new() { "tapestry-survival" },
            ["tapestry-survival"] = new(),
            ["tapestry-rogue"] = new(),
        });

        var emptyProvider = new ServiceCollection().BuildServiceProvider();
        var packs = new PacksModule(emptyProvider, exports, graph, NullLogger<PacksModule>.Instance);
        var rt = new JintRuntime(new IJintApiModule[] { packs }, NullLogger<JintRuntime>.Instance);
        return (rt, exports);
    }

    [Fact]
    public void Call_AcrossDeclaredEdge_ReturnsValue()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('getHungerTier', function (id) { return 'hungry'; }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__r = tapestry.packs.call('@tapestry/survival', 'getHungerTier', 'e1');", "tapestry-cooking");
        rt.Evaluate("__r").Should().Be("hungry");
    }

    [Fact]
    public void Call_MarshalsVariadicArgs()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('add', function (a, b) { return a + b; }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__sum = tapestry.packs.call('@tapestry/survival', 'add', 2, 40);", "tapestry-cooking");
        ((double)rt.Evaluate("__sum")!).Should().Be(42);
    }

    [Fact]
    public void Call_SelfCall_IsExemptFromEdgeCheck()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("""
            tapestry.packs.export('ping', function () { return 'pong'; }, { kind: 'query' });
            globalThis.__self = tapestry.packs.call('@tapestry/survival', 'ping');
            """, "tapestry-survival");
        rt.Evaluate("__self").Should().Be("pong");
    }

    [Fact]
    public void Call_WithoutDeclaredEdge_ThrowsInteropException()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('secret', function () { return 1; }, { kind: 'query' });", "tapestry-survival");
        var act = () => rt.Execute("tapestry.packs.call('@tapestry/survival', 'secret');", "tapestry-rogue");
        act.Should().Throw<InteropException>().WithMessage("*tapestry-rogue*tapestry-survival*");
    }

    [Fact]
    public void Call_UnknownExport_ThrowsInteropException()
    {
        var (rt, _) = CreateRuntime();
        var act = () => rt.Execute("tapestry.packs.call('@tapestry/survival', 'nope');", "tapestry-cooking");
        act.Should().Throw<InteropException>().WithMessage("*nope*");
    }

    [Fact]
    public void Has_DeclaredButNotLoaded_ReturnsFalse()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("globalThis.__h = tapestry.packs.has('@tapestry/survival', 'applyWellFedBuff');", "tapestry-cooking");
        rt.Evaluate("__h").Should().Be(false);
    }

    [Fact]
    public void Has_RegisteredExport_ReturnsTrue()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('getHungerTier', function () {}, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__h = tapestry.packs.has('@tapestry/survival', 'getHungerTier');", "tapestry-cooking");
        rt.Evaluate("__h").Should().Be(true);
    }

    [Fact]
    public void Has_UndeclaredEdge_Throws()
    {
        var (rt, _) = CreateRuntime();
        var act = () => rt.Execute("tapestry.packs.has('@tapestry/survival', 'getHungerTier');", "tapestry-rogue");
        act.Should().Throw<InteropException>();
    }

    [Fact]
    public void Call_CalleeThrows_IsCatchableByCaller()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('boom', function () { throw new Error('kaboom'); }, { kind: 'command' });", "tapestry-survival");
        rt.Execute("""
            globalThis.__caught = false;
            try { tapestry.packs.call('@tapestry/survival', 'boom'); }
            catch (e) { globalThis.__caught = true; }
            """, "tapestry-cooking");
        rt.Evaluate("__caught").Should().Be(true);
    }

    [Fact]
    public void Call_CalleeThrows_Uncaught_PropagatesJavaScriptException()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('boom', function () { throw new Error('kaboom'); }, { kind: 'command' });", "tapestry-survival");
        var act = () => rt.Execute("tapestry.packs.call('@tapestry/survival', 'boom');", "tapestry-cooking");
        act.Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void GetExportRegistry_ReturnsEntryMetadata()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("""
            tapestry.packs.export('getHungerTier', function () {}, {
                kind: 'query',
                description: 'Hunger tier.',
                params: [{ name: 'entityId', type: 'entity' }],
                returns: 'string'
            });
            """, "tapestry-survival");
        rt.Execute("globalThis.__reg = tapestry.packs.getExportRegistry();", "tapestry-survival");
        rt.Evaluate("__reg.length").Should().Be(1d);
        rt.Evaluate("__reg[0].name").Should().Be("getHungerTier");
        rt.Evaluate("__reg[0].pack").Should().Be("tapestry-survival");
        rt.Evaluate("__reg[0].kind").Should().Be("query");
        rt.Evaluate("__reg[0].returns").Should().Be("string");
        rt.Evaluate("__reg[0].appliesTo[0]").Should().Be("all");
    }

    [Fact]
    public void Export_DuplicateName_Throws()
    {
        var (rt, _) = CreateRuntime();
        var act = () => rt.Execute("""
            tapestry.packs.export('dup', function () {}, { kind: 'query' });
            tapestry.packs.export('dup', function () {}, { kind: 'query' });
            """, "tapestry-survival");
        act.Should().Throw<InteropException>().WithMessage("*already*");
    }

    [Fact]
    public void Export_InvalidName_Throws()
    {
        var (rt, _) = CreateRuntime();
        var act = () => rt.Execute("tapestry.packs.export('not-an-identifier', function () {}, { kind: 'query' });", "tapestry-survival");
        act.Should().Throw<Exception>();
    }

    // Regression: an export body that itself calls tapestry.packs.call must be attributed
    // to the EXPORT's pack, not the (now-stale) outer caller. Without InvokeAsPack around
    // the handler invocation, the nested call reads __currentPack = the outer caller and
    // checks the wrong edge. Here cooking calls survival.outer, which calls core.inner:
    // cooking does NOT declare core, but survival DOES — so correct attribution succeeds
    // and misattribution would throw.
    [Fact]
    public void Call_NestedInteropFromExportBody_AttributesToExportsPack()
    {
        var exports = new PackExportRegistry();
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>
        {
            ["tapestry-cooking"] = new() { "tapestry-survival" }, // cooking -> survival only
            ["tapestry-survival"] = new() { "tapestry-core" },     // survival -> core
            ["tapestry-core"] = new(),
        });
        var emptyProvider = new ServiceCollection().BuildServiceProvider();
        var packs = new PacksModule(emptyProvider, exports, graph, NullLogger<PacksModule>.Instance);
        var rt = new JintRuntime(new IJintApiModule[] { packs }, NullLogger<JintRuntime>.Instance);

        rt.Execute("tapestry.packs.export('inner', function () { return 'core-ran'; }, { kind: 'query' });", "tapestry-core");
        rt.Execute("tapestry.packs.export('outer', function () { return tapestry.packs.call('@tapestry/core', 'inner'); }, { kind: 'query' });", "tapestry-survival");

        rt.Execute("globalThis.__nested = tapestry.packs.call('@tapestry/survival', 'outer');", "tapestry-cooking");

        rt.Evaluate("__nested").Should().Be("core-ran");
    }
}
