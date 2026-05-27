// tests/Tapestry.Engine.Tests/Inventory/InventoryManagerDestroyTests.cs
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Inventory;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Inventory;

public class InventoryManagerDestroyTests
{
    private static InventoryManager NewManager(World world)
    {
        var bus = new EventBus();
        var currency = new CurrencyService(world, bus);
        return new InventoryManager(bus, world, currency);
    }

    [Fact]
    public void Destroy_HeldItem_RemovesFromContentsAndUntracks()
    {
        var world = new World();
        var mgr = NewManager(world);

        var holder = new Entity("player", "Hero");
        world.TrackEntity(holder);
        var item = new Entity("item", "a wood chunk");
        holder.AddToContents(item);
        world.TrackEntity(item);

        var ok = mgr.Destroy(holder, item);

        ok.Should().BeTrue();
        holder.Contents.Should().NotContain(item);
        world.GetEntity(item.Id).Should().BeNull();
    }

    [Fact]
    public void Destroy_ItemNotHeld_ReturnsFalse_AndLeavesItem()
    {
        var world = new World();
        var mgr = NewManager(world);

        var holder = new Entity("player", "Hero");
        var stray = new Entity("item", "a rock");
        world.TrackEntity(holder);
        world.TrackEntity(stray);

        var ok = mgr.Destroy(holder, stray);

        ok.Should().BeFalse();
        world.GetEntity(stray.Id).Should().NotBeNull();
    }
}
