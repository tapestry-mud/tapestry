using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Distribution;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests.Distribution;

public class PackValidatorSpawnOnTests
{
    private PackValidator BuildValidator(
        ItemRegistry items,
        TagRegistry tags,
        PackDependencyGraph depGraph,
        IPackManifestProvider manifests)
    {
        var world = new World();
        var eventBus = new EventBus();
        var spawner = new SpawnManager(world, eventBus, new LootTableResolver(), items);
        var propReg = new PropertyRegistry();
        CommonProperties.Register(propReg);
        InventoryProperties.Register(propReg);
        MobProperties.Register(propReg);
        return new PackValidator(
            spawner,
            items,
            world,
            NullLogger<PackValidator>.Instance,
            new AbilityRegistry(),
            new CommandRegistry(),
            tags,
            manifests,
            propReg,
            depGraph);
    }

    private static IPackManifestProvider ManifestWith(PackManifest manifest) =>
        new StaticManifestProvider(new List<PackManifest> { manifest });

    [Fact]
    public void Validate_SpawnOnTag_CrossPackNoDep_Throws()
    {
        var tags = new TagRegistry();
        tags.RegisterPackTag("tapestry-core", "cave_dweller", "Cave-dwelling mob tag.", new[] { "npc" });
        tags.SetDependencyResolver(_ => Enumerable.Empty<string>());

        var depGraph = new PackDependencyGraph();
        depGraph.Build(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "tapestry-tinkers", new List<string>() }
        });

        var items = new ItemRegistry();
        items.Register(new ItemTemplate
        {
            Id = "tapestry-tinkers:copper-chunk",
            Name = "copper",
            Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Tag: "cave_dweller"), 0.15, 1) }
        });

        var manifests = ManifestWith(new PackManifest { Name = "@tapestry/tinkers", Validation = "strict" });
        var validator = BuildValidator(items, tags, depGraph, manifests);

        Assert.Throws<InvalidOperationException>(() => validator.Validate());
    }

    [Fact]
    public void Validate_SpawnOnTag_SamePack_DoesNotThrow()
    {
        var tags = new TagRegistry();
        tags.RegisterPackTag("tapestry-tinkers", "forest_room", "Forest room tag.", new[] { "room" });
        tags.SetDependencyResolver(_ => Enumerable.Empty<string>());

        var depGraph = new PackDependencyGraph();
        depGraph.Build(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "tapestry-tinkers", new List<string>() }
        });

        var items = new ItemRegistry();
        items.Register(new ItemTemplate
        {
            Id = "tapestry-tinkers:wood-chunk",
            Name = "wood",
            Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Tag: "forest_room"), 1.0, 3) }
        });

        var manifests = ManifestWith(new PackManifest { Name = "@tapestry/tinkers", Validation = "strict" });
        var validator = BuildValidator(items, tags, depGraph, manifests);

        validator.Validate();
    }

    [Fact]
    public void Validate_SpawnOnId_CrossPackNoDep_Throws()
    {
        var tags = new TagRegistry();
        tags.SetDependencyResolver(_ => Enumerable.Empty<string>());

        var depGraph = new PackDependencyGraph();
        depGraph.Build(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "tapestry-tinkers", new List<string>() }
        });

        var items = new ItemRegistry();
        items.Register(new ItemTemplate
        {
            Id = "tapestry-tinkers:gem",
            Name = "gem",
            Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Id: "tapestry-core:bat"), 0.1, 1) }
        });

        var manifests = ManifestWith(new PackManifest { Name = "@tapestry/tinkers", Validation = "strict" });
        var validator = BuildValidator(items, tags, depGraph, manifests);

        Assert.Throws<InvalidOperationException>(() => validator.Validate());
    }

    [Fact]
    public void Validate_SpawnOnShop_DoesNotThrow()
    {
        var tags = new TagRegistry();
        tags.SetDependencyResolver(_ => Enumerable.Empty<string>());

        var depGraph = new PackDependencyGraph();
        depGraph.Build(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));

        var items = new ItemRegistry();
        items.Register(new ItemTemplate
        {
            Id = "tapestry-tinkers:rare-gem",
            Name = "gem",
            Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Shop: true), 0.05, 1) }
        });

        var manifests = ManifestWith(new PackManifest { Name = "@tapestry/tinkers", Validation = "strict" });
        var validator = BuildValidator(items, tags, depGraph, manifests);

        validator.Validate();
    }
}

file sealed class StaticManifestProvider : IPackManifestProvider
{
    private readonly IReadOnlyList<PackManifest> _packs;
    public StaticManifestProvider(List<PackManifest> packs) { _packs = packs; }
    public IReadOnlyList<PackManifest> LoadedPacks => _packs;
}
