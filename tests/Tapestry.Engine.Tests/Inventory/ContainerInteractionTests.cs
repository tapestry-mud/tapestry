using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Inventory;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Inventory;

public class ContainerInteractionTests
{
    private (InventoryManager mgr, EventBus bus, World world) Setup()
    {
        var bus = new EventBus();
        var world = new World();
        var currency = new CurrencyService(world, bus);
        return (new InventoryManager(bus, world, currency), bus, world);
    }

    [Fact]
    public void Give_MovesItemBetweenEntities()
    {
        var (mgr, _, _) = Setup();
        var container = new Entity("container", "chest");
        container.AddTag("container");
        var player = new Entity("player", "Rand");

        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        sword.AddTag("sword");
        container.AddToContents(sword);

        // Simulate "get from container" — Give from container to player
        mgr.Give(container, player, sword, silent: true).Should().BeTrue();
        player.Contents.Should().Contain(sword);
        container.Contents.Should().NotContain(sword);
    }

    [Fact]
    public void Give_PutItemInContainer()
    {
        var (mgr, _, _) = Setup();
        var container = new Entity("container", "chest");
        container.AddTag("container");
        var player = new Entity("player", "Rand");

        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        player.AddToContents(sword);

        // Simulate "put in container" — Give from player to container
        mgr.Give(player, container, sword).Should().BeTrue();
        container.Contents.Should().Contain(sword);
        player.Contents.Should().NotContain(sword);
    }

    [Fact]
    public void ContainerContents_ReturnsItemsInside()
    {
        var container = new Entity("container", "corpse");
        container.AddTag("container");

        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        var helm = new Entity("item:armor", "helm");
        helm.AddTag("item");

        container.AddToContents(sword);
        container.AddToContents(helm);

        container.Contents.Should().HaveCount(2);
        container.Contents.Should().Contain(sword);
        container.Contents.Should().Contain(helm);
    }

    [Fact]
    public void Give_FromContainer_BlockedByAccessCheck_StillWorks()
    {
        // Access check is enforced in InventoryModule (scripting layer), not InventoryManager.
        // InventoryManager.Give always succeeds if the item is in source.
        // This test confirms the engine layer doesn't block — pack scripts do policy.
        var (mgr, bus, _) = Setup();

        var container = new Entity("container", "corpse");
        container.AddTag("container");
        container.AddTag("player_corpse");
        container.SetProperty("owner", Guid.NewGuid());

        var player = new Entity("player", "Thief");
        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        container.AddToContents(sword);

        // Engine layer Give works regardless — access checks live in scripting
        mgr.Give(container, player, sword, silent: true).Should().BeTrue();
        player.Contents.Should().Contain(sword);
    }

    [Fact]
    public void CorpseDecay_DumpsContentsToRoom()
    {
        var (mgr, bus, world) = Setup();
        var room = new Room("test", "Test", "Test room");
        world.AddRoom(room);

        var corpse = new Entity("container", "the corpse of a goblin");
        corpse.AddTag("container");
        corpse.AddTag("corpse");
        room.AddEntity(corpse);
        world.TrackEntity(corpse);

        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        corpse.AddToContents(sword);

        var helm = new Entity("item:armor", "helm");
        helm.AddTag("item");
        corpse.AddToContents(helm);

        // Simulate decay: dump contents, remove corpse
        foreach (var item in corpse.Contents.ToList())
        {
            corpse.RemoveFromContents(item);
            room.AddEntity(item);
        }
        room.RemoveEntity(corpse);
        world.UntrackEntity(corpse);

        room.Entities.Should().Contain(sword);
        room.Entities.Should().Contain(helm);
        room.Entities.Should().NotContain(corpse);
    }

    [Fact]
    public void PlayerDeath_TransfersAllGearToCorpse()
    {
        var (mgr, bus, world) = Setup();
        var slots = new SlotRegistry();
        slots.Register(new SlotDefinition("wield", "<wield>", 1));
        slots.Register(new SlotDefinition("head", "<head>", 1));
        var eqMgr = new EquipmentManager(slots, bus);

        var room = new Room("test", "Test", "Test room");
        world.AddRoom(room);

        var player = new Entity("player", "Rand");
        room.AddEntity(player);

        // Give player some gear
        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        sword.SetProperty("slot", "wield");
        player.AddToContents(sword);
        eqMgr.Equip(player, sword, "wield");

        var helm = new Entity("item:armor", "helm");
        helm.AddTag("item");
        player.AddToContents(helm);

        // Simulate death: unequip all, transfer to corpse
        var corpse = new Entity("container", "the corpse of Rand");
        corpse.AddTag("container");
        corpse.AddTag("corpse");
        world.TrackEntity(corpse);

        // Unequip silently
        foreach (var slotKey in player.Equipment.Keys.ToList())
        {
            eqMgr.Unequip(player, slotKey, silent: true);
        }

        // Transfer all inventory silently
        foreach (var item in player.Contents.ToList())
        {
            mgr.Give(player, corpse, item, silent: true);
        }

        room.AddEntity(corpse);

        // Player should be empty
        player.Contents.Should().BeEmpty();
        player.Equipment.Should().BeEmpty();

        // Corpse should have everything
        corpse.Contents.Should().HaveCount(2);
        corpse.Contents.Should().Contain(sword);
        corpse.Contents.Should().Contain(helm);
    }
}
