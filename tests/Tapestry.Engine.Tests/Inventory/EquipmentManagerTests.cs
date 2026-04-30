// tests/Tapestry.Engine.Tests/Inventory/EquipmentManagerTests.cs
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Inventory;

public class EquipmentManagerTests
{
    private (EquipmentManager mgr, EventBus bus) Setup()
    {
        var slots = new SlotRegistry();
        slots.Register(new SlotDefinition("wield", "Wield", 1));
        slots.Register(new SlotDefinition("head", "Head", 1));
        slots.Register(new SlotDefinition("finger", "Finger", 2));
        var bus = new EventBus();
        return (new EquipmentManager(slots, bus), bus);
    }

    [Fact]
    public void Equip_PlacesItemInSlot()
    {
        var (mgr, _) = Setup();
        var player = new Entity("player", "Rand");
        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("sword");
        player.AddToContents(sword);

        var result = mgr.Equip(player, sword, "wield");
        result.Success.Should().BeTrue();
        player.GetEquipment("wield").Should().Be(sword);
        player.Contents.Should().NotContain(sword);
    }

    [Fact]
    public void Equip_AppliesModifiers()
    {
        var (mgr, _) = Setup();
        var player = new Entity("player", "Rand");
        player.Stats.BaseStrength = 10;

        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("sword");
        sword.SetProperty("modifiers", new List<StatModifier>
        {
            new("equipment:sword", StatType.Strength, 5)
        });
        player.AddToContents(sword);

        mgr.Equip(player, sword, "wield");
        player.Stats.Strength.Should().Be(15);
    }

    [Fact]
    public void Unequip_RemovesModifiers()
    {
        var (mgr, _) = Setup();
        var player = new Entity("player", "Rand");
        player.Stats.BaseStrength = 10;

        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("sword");
        sword.SetProperty("modifiers", new List<StatModifier>
        {
            new("equipment:sword", StatType.Strength, 5)
        });
        player.AddToContents(sword);

        mgr.Equip(player, sword, "wield");
        player.Stats.Strength.Should().Be(15);

        mgr.Unequip(player, "wield");
        player.Stats.Strength.Should().Be(10);
        player.GetEquipment("wield").Should().BeNull();
        player.Contents.Should().Contain(sword);
    }

    [Fact]
    public void Equip_FailsIfNotInInventory()
    {
        var (mgr, _) = Setup();
        var player = new Entity("player", "Rand");
        var sword = new Entity("item:weapon", "sword");
        mgr.Equip(player, sword, "wield").Success.Should().BeFalse();
    }

    [Fact]
    public void Equip_FailsIfSlotUnknown()
    {
        var (mgr, _) = Setup();
        var player = new Entity("player", "Rand");
        var jet = new Entity("item:tech", "jetpack");
        player.AddToContents(jet);
        mgr.Equip(player, jet, "jetpack").Success.Should().BeFalse();
    }

    [Fact]
    public void Equip_AutoSwapsWhenSlotFull()
    {
        var (mgr, _) = Setup();
        var player = new Entity("player", "Rand");
        var helm1 = new Entity("item:armor", "iron helm");
        helm1.AddTag("helm");
        var helm2 = new Entity("item:armor", "steel helm");
        helm2.AddTag("helm");
        player.AddToContents(helm1);
        player.AddToContents(helm2);

        mgr.Equip(player, helm1, "head").Success.Should().BeTrue();
        var result = mgr.Equip(player, helm2, "head");
        result.Success.Should().BeTrue();
        result.Displaced.Should().Be(helm1);
        player.GetEquipment("head").Should().Be(helm2);
        player.Contents.Should().Contain(helm1);
    }

    [Fact]
    public void Equip_MultiSlot_AllowsMultiple()
    {
        var (mgr, _) = Setup();
        var player = new Entity("player", "Rand");
        var ring1 = new Entity("item:armor", "gold ring");
        ring1.AddTag("ring");
        var ring2 = new Entity("item:armor", "silver ring");
        ring2.AddTag("ring");
        player.AddToContents(ring1);
        player.AddToContents(ring2);

        mgr.Equip(player, ring1, "finger").Success.Should().BeTrue();
        mgr.Equip(player, ring2, "finger").Success.Should().BeTrue();
    }

    [Fact]
    public void Unequip_Silent_DoesNotFireEvent()
    {
        var (mgr, bus) = Setup();
        var player = new Entity("player", "Rand");
        var helm = new Entity("item:armor", "helm");
        helm.AddTag("item");
        player.AddToContents(helm);

        mgr.Equip(player, helm, "head");

        GameEvent? fired = null;
        bus.Subscribe("entity.unequipped", e => { fired = e; });

        mgr.Unequip(player, "head", silent: true);
        fired.Should().BeNull();
        player.Contents.Should().Contain(helm);
    }

    [Fact]
    public void Equip_FiresEvent()
    {
        var (mgr, bus) = Setup();
        var player = new Entity("player", "Rand");
        var sword = new Entity("item:weapon", "sword");
        sword.AddTag("sword");
        player.AddToContents(sword);

        GameEvent? fired = null;
        bus.Subscribe("entity.equipped", e => { fired = e; });

        mgr.Equip(player, sword, "wield");
        fired.Should().NotBeNull();
        fired!.Data["slot"].Should().Be("wield");
    }
}
