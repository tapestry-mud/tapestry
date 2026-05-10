using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests;

public class PackValidatorTagTests
{
    private sealed class FakeManifestProvider : IPackManifestProvider
    {
        private readonly List<PackManifest> _manifests = new();
        public IReadOnlyList<PackManifest> LoadedPacks => _manifests;
        public void Add(string packName, bool lenient = false)
        {
            _manifests.Add(new PackManifest { Name = packName, TagValidation = lenient ? "lenient" : "strict" });
        }
    }

    private static TagRegistry BaseRegistry()
    {
        var r = new TagRegistry();
        r.RegisterEngineTag("no_kill", "Can be targeted", ["npc"]);
        r.RegisterEngineTag("equippable", "Can be equipped", ["item"]);
        r.RegisterEngineTag("safe", "No combat in room", ["room"]);
        r.RegisterEngineTag("regen", "Eligible for regen", ["npc", "player"]);
        return r;
    }

    private static (PackValidator Validator, SpawnManager SpawnManager, ItemRegistry ItemRegistry, World World)
        CreateValidator(TagRegistry registry, FakeManifestProvider manifests)
    {
        var world = new World();
        var eventBus = new EventBus();
        var itemRegistry = new ItemRegistry();
        var spawnManager = new SpawnManager(world, eventBus, new LootTableResolver(), itemRegistry);
        var validator = new PackValidator(
            spawnManager,
            itemRegistry,
            world,
            NullLogger<PackValidator>.Instance,
            new AbilityRegistry(),
            new CommandRegistry(),
            registry,
            manifests);
        return (validator, spawnManager, itemRegistry, world);
    }

    [Fact]
    public void Validate_PassesWhenAllTagsKnownAndMatchEntityType()
    {
        var manifests = new FakeManifestProvider();
        manifests.Add("my-pack");
        var (validator, spawnManager, _, _) = CreateValidator(BaseRegistry(), manifests);
        var mob = new MobTemplate { Id = "my-pack:guard", Type = "npc", Tags = ["no_kill"] };
        spawnManager.RegisterTemplate(mob);

        var act = () => validator.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ThrowsInStrictMode_OnUnknownMobTag()
    {
        var manifests = new FakeManifestProvider();
        manifests.Add("my-pack", lenient: false);
        var (validator, spawnManager, _, _) = CreateValidator(BaseRegistry(), manifests);
        var mob = new MobTemplate { Id = "my-pack:guard", Type = "npc", Tags = ["no_kill", "typo_tag"] };
        spawnManager.RegisterTemplate(mob);

        var act = () => validator.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*typo_tag*my-pack:guard*");
    }

    [Fact]
    public void Validate_DoesNotThrowInLenientMode_OnUnknownMobTag()
    {
        var manifests = new FakeManifestProvider();
        manifests.Add("my-pack", lenient: true);
        var (validator, spawnManager, _, _) = CreateValidator(BaseRegistry(), manifests);
        var mob = new MobTemplate { Id = "my-pack:guard", Type = "npc", Tags = ["no_kill", "unknown_tag"] };
        spawnManager.RegisterTemplate(mob);

        var act = () => validator.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_AlwaysThrows_WhenTagUsedOnWrongEntityType_EvenInLenientMode()
    {
        var manifests = new FakeManifestProvider();
        manifests.Add("my-pack", lenient: true);
        var (validator, spawnManager, _, _) = CreateValidator(BaseRegistry(), manifests);
        var mob = new MobTemplate { Id = "my-pack:guard", Type = "npc", Tags = ["safe"] }; // safe is room-only
        spawnManager.RegisterTemplate(mob);

        var act = () => validator.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*safe*my-pack:guard*room*");
    }

    [Fact]
    public void Validate_ThrowsInStrictMode_OnUnknownItemTag()
    {
        var manifests = new FakeManifestProvider();
        manifests.Add("my-pack", lenient: false);
        var (validator, _, itemRegistry, _) = CreateValidator(BaseRegistry(), manifests);
        var item = new ItemTemplate { Id = "my-pack:sword", Tags = ["equippable", "nonexistent"] };
        itemRegistry.Register(item);

        var act = () => validator.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*nonexistent*my-pack:sword*");
    }

    [Fact]
    public void Validate_ThrowsInStrictMode_OnUnknownRoomTag()
    {
        var manifests = new FakeManifestProvider();
        manifests.Add("my-pack", lenient: false);
        var (validator, _, _, world) = CreateValidator(BaseRegistry(), manifests);
        var room = new Room("my-pack:lobby", "Lobby", "A room.");
        room.AddTag("typo_tg");
        world.AddRoom(room);

        var act = () => validator.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*typo_tg*my-pack:lobby*");
    }

    [Fact]
    public void Validate_PerPackMode_StrictPackThrows_LenientPackDoesNot()
    {
        var manifests = new FakeManifestProvider();
        manifests.Add("strict-pack", lenient: false);
        manifests.Add("lenient-pack", lenient: true);

        var registry = BaseRegistry();
        var (validator, spawnManager, _, _) = CreateValidator(registry, manifests);

        var strictMob = new MobTemplate { Id = "strict-pack:guard", Type = "npc", Tags = ["no_kill", "unknown_tag"] };
        var lenientMob = new MobTemplate { Id = "lenient-pack:guide", Type = "npc", Tags = ["no_kill", "unknown_tag"] };
        spawnManager.RegisterTemplate(strictMob);
        spawnManager.RegisterTemplate(lenientMob);

        var act = () => validator.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*unknown_tag*strict-pack:guard*");
    }
}
