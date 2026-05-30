using System.Collections.Generic;
using FluentAssertions;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Interop;

public class PackDependencyGraphTests
{
    private static PackDependencyGraph Build()
    {
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>
        {
            ["tapestry-cooking"] = new() { "tapestry-core", "tapestry-survival" },
            ["tapestry-survival"] = new() { "tapestry-core" },
            ["tapestry-core"] = new(),
        });
        return graph;
    }

    [Fact]
    public void DeclaresEdge_TrueForDeclaredDependency()
    {
        Build().DeclaresEdge("tapestry-cooking", "tapestry-survival").Should().BeTrue();
    }

    [Fact]
    public void DeclaresEdge_FalseForUndeclaredDependency()
    {
        Build().DeclaresEdge("tapestry-survival", "tapestry-cooking").Should().BeFalse();
    }

    [Fact]
    public void DeclaresEdge_FalseForUnknownCaller()
    {
        Build().DeclaresEdge("tapestry-unknown", "tapestry-core").Should().BeFalse();
    }

    [Fact]
    public void IsLoaded_TrueForPacksInTheMap()
    {
        var g = Build();
        g.IsLoaded("tapestry-survival").Should().BeTrue();
        g.IsLoaded("tapestry-not-loaded").Should().BeFalse();
    }

    [Fact]
    public void IsCaseInsensitive()
    {
        Build().DeclaresEdge("Tapestry-Cooking", "Tapestry-Survival").Should().BeTrue();
    }

    [Fact]
    public void TopologicalOrder_PlacesDependenciesBeforeDependents()
    {
        Build().TopologicalOrder().Should().Equal("tapestry-core", "tapestry-survival", "tapestry-cooking");
    }

    [Fact]
    public void TopologicalOrder_DependentLoadsLast_EvenWhenAlphabeticallyFirst()
    {
        // The real bug: alphabetical directory order loads @mallek before @tapestry,
        // but a world pack that depends on @tapestry/* must load AFTER them.
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>
        {
            ["mallek-legends-forgotten"] = new() { "tapestry-core", "tapestry-tinkers" },
            ["tapestry-tinkers"] = new() { "tapestry-core" },
            ["tapestry-core"] = new(),
        });

        graph.TopologicalOrder().Should().Equal("tapestry-core", "tapestry-tinkers", "mallek-legends-forgotten");
    }

    [Fact]
    public void TopologicalOrder_BreaksTiesAlphabetically()
    {
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>
        {
            ["tapestry-cooking"] = new() { "tapestry-core" },
            ["tapestry-survival"] = new() { "tapestry-core" },
            ["tapestry-core"] = new(),
        });

        graph.TopologicalOrder().Should().Equal("tapestry-core", "tapestry-cooking", "tapestry-survival");
    }

    [Fact]
    public void TopologicalOrder_IgnoresEdgesToUnloadedPacks()
    {
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>
        {
            // depends on a pack that was never loaded — must not appear in the order
            ["tapestry-core"] = new() { "tapestry-not-loaded" },
        });

        graph.TopologicalOrder().Should().Equal("tapestry-core");
    }

    [Fact]
    public void TopologicalOrder_DoesNotHangOnCycle()
    {
        var graph = new PackDependencyGraph();
        graph.Build(new Dictionary<string, List<string>>
        {
            ["a"] = new() { "b" },
            ["b"] = new() { "a" },
        });

        // best-effort: every loaded pack still appears exactly once
        graph.TopologicalOrder().Should().BeEquivalentTo(new[] { "a", "b" });
    }
}
