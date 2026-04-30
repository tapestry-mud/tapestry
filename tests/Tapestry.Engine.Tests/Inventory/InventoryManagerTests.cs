// tests/Tapestry.Engine.Tests/Inventory/InventoryManagerTests.cs
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Inventory;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Inventory;

public class InventoryManagerTests
{
    private (InventoryManager mgr, EventBus bus, World world) Setup()
    {
        var bus = new EventBus();
        var world = new World();
        var currency = new CurrencyService(world, bus);
        return (new InventoryManager(bus, world, currency), bus, world);
    }

    [Fact]
    public void PickUp_MovesItemToInventory()
    {
        var (mgr, _, world) = Setup();
        var room = new Room("test", "Test", "Test room");
        world.AddRoom(room);

        var player = new Entity("player", "Rand");
        room.AddEntity(player);
        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        room.AddEntity(sword);

        mgr.PickUp(player, sword).Should().BeTrue();
        player.Contents.Should().Contain(sword);
        room.Entities.Should().NotContain(sword);
    }

    [Fact]
    public void PickUp_FailsIfOverWeight()
    {
        var (mgr, _, world) = Setup();
        var room = new Room("test", "Test", "Test room");
        world.AddRoom(room);

        var player = new Entity("player", "Rand");
        player.Stats.BaseStrength = 10;
        player.SetProperty("max_carry_weight", 20);
        room.AddEntity(player);

        var boulder = new Entity("item:misc", "boulder");
        boulder.AddTag("item");
        boulder.SetProperty("weight", 25);
        room.AddEntity(boulder);

        mgr.PickUp(player, boulder).Should().BeFalse();
    }

    [Fact]
    public void Drop_MovesItemToRoom()
    {
        var (mgr, _, world) = Setup();
        var room = new Room("test", "Test", "Test room");
        world.AddRoom(room);

        var player = new Entity("player", "Rand");
        room.AddEntity(player);
        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        player.AddToContents(sword);

        mgr.Drop(player, sword).Should().BeTrue();
        room.Entities.Should().Contain(sword);
        player.Contents.Should().NotContain(sword);
    }

    [Fact]
    public void PickUp_FiresEvent()
    {
        var (mgr, bus, world) = Setup();
        var room = new Room("test", "Test", "Test room");
        world.AddRoom(room);

        var player = new Entity("player", "Rand");
        room.AddEntity(player);
        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        room.AddEntity(sword);

        GameEvent? fired = null;
        bus.Subscribe("entity.item.picked_up", e => { fired = e; });

        mgr.PickUp(player, sword);
        fired.Should().NotBeNull();
    }

    [Fact]
    public void CarryWeight_CalculatesCorrectly()
    {
        var player = new Entity("player", "Rand");
        var s1 = new Entity("item", "sword");
        s1.SetProperty("weight", 5);
        var s2 = new Entity("item", "shield");
        s2.SetProperty("weight", 10);
        player.AddToContents(s1);
        player.AddToContents(s2);

        InventoryManager.GetCarryWeight(player).Should().Be(15);
    }

    [Fact]
    public void PickUp_Silent_DoesNotFireEvent()
    {
        var (mgr, bus, world) = Setup();
        var room = new Room("test", "Test", "Test room");
        world.AddRoom(room);

        var player = new Entity("player", "Rand");
        room.AddEntity(player);
        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("item");
        room.AddEntity(sword);

        GameEvent? fired = null;
        bus.Subscribe("entity.item.picked_up", e => { fired = e; });

        mgr.PickUp(player, sword, silent: true);
        fired.Should().BeNull();
    }

    [Fact]
    public void PickUp_WorksForContainerTaggedEntities()
    {
        var (mgr, _, world) = Setup();
        var room = new Room("test", "Test", "Test room");
        world.AddRoom(room);

        var player = new Entity("player", "Rand");
        room.AddEntity(player);
        var corpse = new Entity("container", "the corpse of a goblin");
        corpse.AddTag("container");
        corpse.AddTag("corpse");
        room.AddEntity(corpse);

        mgr.PickUp(player, corpse).Should().BeTrue();
        player.Contents.Should().Contain(corpse);
    }
}
