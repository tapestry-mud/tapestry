// tests/Tapestry.Engine.Tests/Quests/EventPayloadTests.cs
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Inventory;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Quests;

public class EventPayloadTests
{
    private (InventoryManager mgr, EventBus bus, World world, Room room) SetupInventory()
    {
        var bus = new EventBus();
        var world = new World();
        var currency = new CurrencyService(world, bus);
        var mgr = new InventoryManager(bus, world, currency);
        var room = new Room("test:room", "Test Room", "A test room.");
        world.AddRoom(room);
        return (mgr, bus, world, room);
    }

    private (CombatManager combat, EventBus bus, World world, Room room) SetupCombat()
    {
        var bus = new EventBus();
        var world = new World();
        var combat = new CombatManager(world, bus);
        var room = new Room("test:arena", "Arena", "A test arena.");
        world.AddRoom(room);
        return (combat, bus, world, room);
    }

    private Entity CreatePlayer(World world, Room room, string name = "Travis")
    {
        var entity = new Entity("player", name);
        entity.AddTag("player");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 100;
        entity.Stats.BaseStrength = 10;
        entity.Stats.BaseDexterity = 10;
        room.AddEntity(entity);
        world.TrackEntity(entity);
        return entity;
    }

    private Entity CreateMob(World world, Room room, string name = "a goblin", string templateId = "core:goblin")
    {
        var entity = new Entity("npc", name);
        entity.AddTag("npc");
        entity.AddTag("killable");
        entity.Stats.BaseMaxHp = 40;
        entity.Stats.Hp = 40;
        entity.Stats.BaseStrength = 8;
        entity.Stats.BaseDexterity = 10;
        entity.SetProperty(CommonProperties.TemplateId, templateId);
        room.AddEntity(entity);
        world.TrackEntity(entity);
        return entity;
    }

    private Entity CreateItem(Room room, string name = "rusty dagger", string templateId = "core:rusty-dagger")
    {
        var item = new Entity("item:weapon", name);
        item.AddTag("item");
        item.SetProperty(CommonProperties.TemplateId, templateId);
        room.AddEntity(item);
        return item;
    }

    [Fact]
    public void ItemPickedUp_EventContainsTemplateId()
    {
        var (mgr, bus, world, room) = SetupInventory();
        var player = new Entity("player", "Travis");
        room.AddEntity(player);
        var item = CreateItem(room, "rusty dagger", "core:rusty-dagger");

        GameEvent? fired = null;
        bus.Subscribe("entity.item.picked_up", e => { fired = e; });

        mgr.PickUp(player, item);

        fired.Should().NotBeNull();
        fired!.Data.Should().ContainKey("templateId");
        fired.Data["templateId"].Should().Be("core:rusty-dagger");
    }

    [Fact]
    public void ItemGiven_EventContainsTemplateId()
    {
        var (mgr, bus, world, room) = SetupInventory();
        var giver = new Entity("player", "Travis");
        room.AddEntity(giver);
        var receiver = new Entity("npc", "shopkeeper");
        room.AddEntity(receiver);
        var item = new Entity("item:weapon", "rusty dagger");
        item.AddTag("item");
        item.SetProperty(CommonProperties.TemplateId, "core:rusty-dagger");
        giver.AddToContents(item);

        GameEvent? fired = null;
        bus.Subscribe("entity.item.given", e => { fired = e; });

        mgr.Give(giver, receiver, item);

        fired.Should().NotBeNull();
        fired!.Data.Should().ContainKey("templateId");
        fired.Data["templateId"].Should().Be("core:rusty-dagger");
    }

    [Fact]
    public void MobKilled_EventFires_WithKillerAndTemplateId()
    {
        var (combat, bus, world, room) = SetupCombat();
        var player = CreatePlayer(world, room);
        var mob = CreateMob(world, room, "a goblin", "core:goblin");
        combat.Engage(player, mob);

        GameEvent? mobKilledEvent = null;
        bus.Subscribe("mob.killed", e => { mobKilledEvent = e; });

        combat.HandleEntityDeath(mob.Id, player.Id);

        mobKilledEvent.Should().NotBeNull();
        mobKilledEvent!.SourceEntityId.Should().Be(player.Id);
        mobKilledEvent.TargetEntityId.Should().Be(mob.Id);
        mobKilledEvent.Data.Should().ContainKey("templateId");
        mobKilledEvent.Data["templateId"].Should().Be("core:goblin");
        mobKilledEvent.Data.Should().ContainKey("mobName");
        mobKilledEvent.Data["mobName"].Should().Be("a goblin");
    }
}
