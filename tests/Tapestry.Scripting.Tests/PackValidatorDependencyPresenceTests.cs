using System;
using System.Collections.Generic;
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

public class PackValidatorDependencyPresenceTests
{
    // Builds a PackValidator over empty content registries, so only the dependency-presence
    // and interop-resolution passes do meaningful work.
    private static PackValidator BuildValidator(
        PackDependencyGraph graph,
        IPackManifestProvider manifests)
    {
        var world = new World();
        var items = new ItemRegistry();
        var spawner = new SpawnManager(world, new EventBus(), new LootTableResolver(), items);
        var tags = new TagRegistry();
        tags.SetDependencyResolver(_ => System.Linq.Enumerable.Empty<string>());
        var props = new PropertyRegistry();
        return new PackValidator(
            spawner, items, world, NullLogger<PackValidator>.Instance,
            new AbilityRegistry(), new CommandRegistry(), tags, manifests, props,
            graph);
    }

    private static IPackManifestProvider Manifests(params PackManifest[] m) =>
        new StaticManifestProviderForDeps(m);

    private static PackDependencyGraph GraphLoading(params string[] loadedNamespaces)
    {
        var graph = new PackDependencyGraph();
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ns in loadedNamespaces) { map[ns] = new List<string>(); }
        graph.Build(map);
        return graph;
    }

    [Fact]
    public void Validate_MissingRequiredDependency_Throws()
    {
        // cooking declares a required dep on survival, but only cooking is loaded.
        var manifests = Manifests(new PackManifest
        {
            Name = "@tapestry/cooking",
            Dependencies = new() { ["@tapestry/survival"] = "^1.0.0" }
        });
        var validator = BuildValidator(
            GraphLoading("tapestry-cooking"), manifests);

        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate());
        Assert.Contains("tapestry-survival", ex.Message);
        Assert.Contains("not loaded", ex.Message);
    }

    [Fact]
    public void Validate_PresentRequiredDependency_Passes()
    {
        var manifests = Manifests(new PackManifest
        {
            Name = "@tapestry/cooking",
            Dependencies = new() { ["@tapestry/survival"] = "^1.0.0" }
        });
        var validator = BuildValidator(
            GraphLoading("tapestry-cooking", "tapestry-survival"), manifests);

        validator.Validate(); // does not throw
    }

    [Fact]
    public void Validate_AbsentOptionalDependency_Passes()
    {
        var manifests = Manifests(new PackManifest
        {
            Name = "@tapestry/cooking",
            OptionalDependencies = new() { ["@tapestry/survival"] = "^1.0.0" }
        });
        var validator = BuildValidator(
            GraphLoading("tapestry-cooking"), manifests);

        validator.Validate(); // optional + absent => fine
    }
}

file sealed class StaticManifestProviderForDeps : IPackManifestProvider
{
    private readonly IReadOnlyList<PackManifest> _packs;
    public StaticManifestProviderForDeps(IReadOnlyList<PackManifest> packs) { _packs = packs; }
    public IReadOnlyList<PackManifest> LoadedPacks => _packs;
}
