using FluentAssertions;
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

namespace Tapestry.Scripting.Tests;

/// <summary>
/// A runtime-created namespace (solo-oracle destination pack, restored by
/// RuntimeNamespaceStore at boot) has no manifest to carry `validation: lenient`, so it
/// used to fall through to strict and crash the boot on any pack-declared property that
/// rode a generated room ("unregistered property oracle_populated" was the witnessed
/// case). These tests pin the fix: runtime namespaces validate lenient; manifest-less
/// namespaces that are NOT runtime-created stay strict.
/// </summary>
public class PackValidatorRuntimeNamespaceTests
{
    private sealed class FakeManifestProvider : IPackManifestProvider
    {
        private readonly List<PackManifest> _manifests = new();
        public IReadOnlyList<PackManifest> LoadedPacks => _manifests;
        public void Add(string packName, bool lenient = false)
        {
            _manifests.Add(new PackManifest { Name = packName, Validation = lenient ? "lenient" : "strict" });
        }
    }

    private static TagRegistry BaseRegistry()
    {
        var r = new TagRegistry();
        r.RegisterEngineTag("safe", "No combat in room", ["room"]);
        return r;
    }

    private static RuntimeNamespaceStore NewStore(string? dataRoot = null)
    {
        dataRoot ??= Path.Combine(Path.GetTempPath(), "tapestry-test-" + Guid.NewGuid().ToString("N"));
        return new RuntimeNamespaceStore(dataRoot, new LoadedPackNamespaces(), NullLogger<RuntimeNamespaceStore>.Instance);
    }

    private static (PackValidator Validator, World World) CreateValidator(
        FakeManifestProvider manifests, RuntimeNamespaceStore? store)
    {
        var world = new World();
        var eventBus = new EventBus();
        var itemRegistry = new ItemRegistry();
        var spawnManager = new SpawnManager(world, eventBus, new LootTableResolver(), itemRegistry);
        var propertyRegistry = new PropertyRegistry();
        CommonProperties.Register(propertyRegistry);
        var validator = new PackValidator(
            spawnManager,
            itemRegistry,
            world,
            NullLogger<PackValidator>.Instance,
            new AbilityRegistry(),
            new CommandRegistry(),
            BaseRegistry(),
            manifests,
            propertyRegistry,
            new PackDependencyGraph(),
            store);
        return (validator, world);
    }

    [Fact]
    public void RuntimeNamespace_UnregisteredRoomProperty_WarnsInsteadOfThrowing()
    {
        var store = NewStore();
        store.Register("scratch-solo");
        var (validator, world) = CreateValidator(new FakeManifestProvider(), store);
        var room = new Room("scratch-solo:room-1", "Room", "Desc.");
        room.SetProperty("oracle_populated", true); // nothing registers this
        world.AddRoom(room);

        var act = () => validator.Validate();

        act.Should().NotThrow(); // lenient: warn + count, no boot crash
    }

    [Fact]
    public void RuntimeNamespace_UnknownRoomTag_WarnsInsteadOfThrowing()
    {
        var store = NewStore();
        store.Register("scratch-solo");
        var (validator, world) = CreateValidator(new FakeManifestProvider(), store);
        var room = new Room("scratch-solo:room-1", "Room", "Desc.");
        room.AddTag("mystery_tag");
        world.AddRoom(room);

        var act = () => validator.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void RuntimeNamespace_RestoredViaLoadAtBoot_IsAlsoLenient()
    {
        // Session 1 registers the namespace (writes the marker); session 2 restores it at
        // boot. The restored path is the one the witnessed crash went through.
        var dataRoot = Path.Combine(Path.GetTempPath(), "tapestry-test-" + Guid.NewGuid().ToString("N"));
        NewStore(dataRoot).Register("scratch-solo");

        var rebooted = NewStore(dataRoot);
        rebooted.LoadAtBoot();

        var (validator, world) = CreateValidator(new FakeManifestProvider(), rebooted);
        var room = new Room("scratch-solo:room-1", "Room", "Desc.");
        room.SetProperty("oracle_populated", true);
        world.AddRoom(room);

        var act = () => validator.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void NonRuntimeNamespaceWithoutManifest_StillValidatesStrict()
    {
        // A manifest-less namespace that was NOT runtime-created (e.g. a pack removed from
        // server.yaml whose side-cars linger) must keep failing strict — the fix must not
        // blanket-lenient every unknown namespace.
        var (validator, world) = CreateValidator(new FakeManifestProvider(), NewStore());
        var room = new Room("ghost-pack:room-1", "Room", "Desc.");
        room.SetProperty("oracle_populated", true);
        world.AddRoom(room);

        var act = () => validator.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*oracle_populated*");
    }

    [Fact]
    public void NullStore_KeepsStrictBehavior()
    {
        var (validator, world) = CreateValidator(new FakeManifestProvider(), store: null);
        var room = new Room("scratch-solo:room-1", "Room", "Desc.");
        room.SetProperty("oracle_populated", true);
        world.AddRoom(room);

        var act = () => validator.Validate();

        act.Should().Throw<InvalidOperationException>();
    }
}
