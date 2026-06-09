using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class PacksRequireTests
{
    private static (JintRuntime rt, PackExportRegistry exports) CreateRuntime()
    {
        var exports = new PackExportRegistry();
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>
        {
            ["tapestry-cooking"] = new() { "tapestry-survival" },
            ["tapestry-survival"] = new() { "tapestry-core" },
            ["tapestry-core"] = new(),
            ["tapestry-rogue"] = new(),
        });
        var emptyProvider = new ServiceCollection().BuildServiceProvider();
        var packs = new PacksModule(emptyProvider, exports, graph, NullLogger<PacksModule>.Instance);
        var rt = new JintRuntime(new IJintApiModule[] { packs }, NullLogger<JintRuntime>.Instance);
        return (rt, exports);
    }

    [Fact]
    public void Require_AcrossDeclaredEdge_ResolvesMemberAtCallTime()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('getHungerTier', function (id) { return 'hungry'; }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__r = tapestry.packs.require('@tapestry/survival').getHungerTier('e1');", "tapestry-cooking");
        rt.Evaluate("__r").Should().Be("hungry");
    }

    // THE late-binding guarantee: require() (and even holding the proxy) BEFORE the
    // export exists; the member resolves later, at access time.
    [Fact]
    public void Require_BeforeExportRegistered_MemberResolvesLater()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("globalThis.__survival = tapestry.packs.require('@tapestry/survival');", "tapestry-cooking");
        rt.Evaluate("typeof __survival.getHungerTier").Should().Be("undefined");
        rt.Execute("tapestry.packs.export('getHungerTier', function (id) { return 'hungry'; }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__late = __survival.getHungerTier('e1');", "tapestry-cooking");
        rt.Evaluate("__late").Should().Be("hungry");
    }

    // Intra-pack self-import: importer "file" runs before the exporter file. No self-dep needed.
    [Fact]
    public void Require_SelfImport_ImporterFileBeforeExporterFile_Works()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("globalThis.__self = tapestry.packs.require('@tapestry/survival');", "tapestry-survival");
        rt.Execute("tapestry.packs.export('helper', function () { return 42; }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__v = __self.helper();", "tapestry-survival");
        ((double)rt.Evaluate("__v")!).Should().Be(42);
    }

    [Fact]
    public void Require_WithoutDeclaredEdge_ThrowsAtRequireTime()
    {
        var (rt, _) = CreateRuntime();
        var act = () => rt.Execute("tapestry.packs.require('@tapestry/survival');", "tapestry-rogue");
        act.Should().Throw<InteropException>().WithMessage("*tapestry-rogue*tapestry-survival*");
    }

    // The edge is enforced LIVE per member access: a proxy legitimately obtained by
    // cooking must not work when the current pack context is rogue (no edge).
    [Fact]
    public void Require_ProxyLeakedToEdgelessPack_MemberAccessThrows()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('secret', function () { return 1; }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__leak = tapestry.packs.require('@tapestry/survival');", "tapestry-cooking");
        var act = () => rt.Execute("__leak.secret();", "tapestry-rogue");
        act.Should().Throw<InteropException>().WithMessage("*tapestry-rogue*tapestry-survival*");
    }

    [Fact]
    public void Require_NamespaceExport_MembersReadable()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('tiers', { FULL_MIN: 67, HUNGRY_MIN: 34 }, {});", "tapestry-survival");
        rt.Execute("globalThis.__t = tapestry.packs.require('@tapestry/survival').tiers.FULL_MIN;", "tapestry-cooking");
        ((double)rt.Evaluate("__t")!).Should().Be(67);
    }

    [Fact]
    public void Require_MissingMember_IsUndefined()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("globalThis.__u = typeof tapestry.packs.require('@tapestry/survival').nope;", "tapestry-cooking");
        rt.Evaluate("__u").Should().Be("undefined");
    }

    [Fact]
    public void Require_AssigningToProxy_Throws()
    {
        var (rt, _) = CreateRuntime();
        var act = () => rt.Execute("tapestry.packs.require('@tapestry/survival').x = 5;", "tapestry-cooking");
        act.Should().Throw<InteropException>().WithMessage("*read-only*");
    }

    // Function exports invoked via the proxy run attributed to the EXPORTER's pack
    // (InvokeAsPack), so nested interop gates against the exporter's edges.
    // cooking -> survival only; survival -> core. Misattribution would throw.
    [Fact]
    public void Require_FunctionExport_RunsAttributedToExporterPack()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('inner', function () { return 'core-ran'; }, { kind: 'query' });", "tapestry-core");
        rt.Execute("tapestry.packs.export('outer', function () { return tapestry.packs.call('@tapestry/core', 'inner'); }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__n = tapestry.packs.require('@tapestry/survival').outer();", "tapestry-cooking");
        rt.Evaluate("__n").Should().Be("core-ran");
    }

    [Fact]
    public void Require_ObjectArgument_PassesThroughIntact()
    {
        var (rt, _) = CreateRuntime();
        rt.Execute("""
            globalThis.__received = null;
            tapestry.packs.export('register', function (recipe) {
                globalThis.__received = recipe;
                return recipe.id;
            }, { kind: 'command' });
            """, "tapestry-survival");
        rt.Execute("globalThis.__ret = tapestry.packs.require('@tapestry/survival').register({ id: 'r1', inputs: [{ count: 2 }] });", "tapestry-cooking");
        rt.Evaluate("__ret").Should().Be("r1");
        ((double)rt.Evaluate("__received.inputs[0].count")!).Should().Be(2);
    }
}
