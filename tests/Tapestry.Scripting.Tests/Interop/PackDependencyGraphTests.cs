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
}
