using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class PacksExportPolicyTests
{
    private static (JintRuntime rt, PackExportRegistry exports, RegistrationPolicy policy) CreateRuntime()
    {
        var exports = new PackExportRegistry();
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>
        {
            ["tapestry-cooking"] = new() { "tapestry-survival" },
            ["tapestry-survival"] = new(),
        });
        var policy = new RegistrationPolicy(graph);
        var emptyProvider = new ServiceCollection().BuildServiceProvider();
        var packs = new PacksModule(emptyProvider, exports, graph, NullLogger<PacksModule>.Instance, policy);
        var rt = new JintRuntime(new IJintApiModule[] { packs }, NullLogger<JintRuntime>.Instance);
        return (rt, exports, policy);
    }

    // Eager visibility: load-time interop sees an export before any seal (shipped
    // v0.1.9 behavior, relied on by dependency-ordered load-time packs.call).
    [Fact]
    public void Export_VisibleImmediately_PreSeal()
    {
        var (rt, _, _) = CreateRuntime();
        rt.Execute("tapestry.packs.export('eager', function () { return 'now'; }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__e = tapestry.packs.call('@tapestry/survival', 'eager');", "tapestry-cooking");
        rt.Evaluate("__e").Should().Be("now");
    }

    // The v0.1.19-style protection, relocated: undeclared duplicate = loud boot error at the seal.
    [Fact]
    public void Export_DuplicateName_FailsBootAtSeal()
    {
        var (rt, _, policy) = CreateRuntime();
        rt.Execute("""
            tapestry.packs.export('dup', function () { return 1; }, { kind: 'query' });
            tapestry.packs.export('dup', function () { return 2; }, { kind: 'query' });
            """, "tapestry-survival");
        var act = () => policy.Resolve();
        act.Should().Throw<InvalidOperationException>().WithMessage("*export*dup*");
    }

    [Fact]
    public void Export_SameOwnerOverride_WinsAtSeal()
    {
        var (rt, _, policy) = CreateRuntime();
        rt.Execute("""
            tapestry.packs.export('impl', function () { return 'base'; }, { kind: 'query' });
            tapestry.packs.export('impl', function () { return 'override'; }, { kind: 'query', override: true });
            """, "tapestry-survival");
        policy.Resolve();
        rt.Execute("globalThis.__w = tapestry.packs.call('@tapestry/survival', 'impl');", "tapestry-cooking");
        rt.Evaluate("__w").Should().Be("override");
    }

    // Order-independence: override exported BEFORE its base; the seal still picks the override.
    [Fact]
    public void Export_OverrideBeforeBase_SealStillPicksOverride()
    {
        var (rt, _, policy) = CreateRuntime();
        rt.Execute("""
            tapestry.packs.export('impl', function () { return 'override'; }, { kind: 'query', override: true });
            tapestry.packs.export('impl', function () { return 'base'; }, { kind: 'query' });
            """, "tapestry-survival");
        policy.Resolve();
        rt.Execute("globalThis.__w2 = tapestry.packs.call('@tapestry/survival', 'impl');", "tapestry-cooking");
        rt.Evaluate("__w2").Should().Be("override");
    }

    // Cross-pack same export name never collides: registry keying is (pack, name).
    [Fact]
    public void Export_SameNameDifferentPacks_BothSurviveSeal()
    {
        var (rt, exports, policy) = CreateRuntime();
        rt.Execute("tapestry.packs.export('init', function () { return 's'; }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("tapestry.packs.export('init', function () { return 'c'; }, { kind: 'query' });", "tapestry-cooking");
        policy.Resolve();
        exports.Has("tapestry-survival", "init").Should().BeTrue();
        exports.Has("tapestry-cooking", "init").Should().BeTrue();
    }

    // D5 end-to-end: a post-seal export (runtime entity-script territory) binds
    // immediately and is callable through the normal gated surface.
    [Fact]
    public void Export_PostSeal_ResolvesImmediately()
    {
        var (rt, _, policy) = CreateRuntime();
        policy.Resolve();
        rt.Execute("tapestry.packs.export('late', function () { return 'rt'; }, { kind: 'query' });", "tapestry-survival");
        rt.Execute("globalThis.__l = tapestry.packs.call('@tapestry/survival', 'late');", "tapestry-cooking");
        rt.Evaluate("__l").Should().Be("rt");
    }

    [Fact]
    public void Export_PostSeal_UndeclaredDuplicate_Throws()
    {
        var (rt, _, policy) = CreateRuntime();
        rt.Execute("tapestry.packs.export('taken', function () { return 1; }, { kind: 'query' });", "tapestry-survival");
        policy.Resolve();
        var act = () => rt.Execute("tapestry.packs.export('taken', function () { return 2; }, { kind: 'query' });", "tapestry-survival");
        act.Should().Throw<InvalidOperationException>().WithMessage("*taken*");
    }
}
