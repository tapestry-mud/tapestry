// tests/Tapestry.Engine.Tests/Inventory/SlotRegistryTests.cs
using FluentAssertions;
using Tapestry.Engine.Inventory;

namespace Tapestry.Engine.Tests.Inventory;

public class SlotRegistryTests
{
    [Fact]
    public void Register_AndGetSlot()
    {
        var registry = new SlotRegistry();
        registry.Register(new SlotDefinition("head", "Head", 1));
        registry.GetSlot("head").Should().NotBeNull();
        registry.GetSlot("head")!.Display.Should().Be("Head");
    }

    [Fact]
    public void Register_MultiSlot()
    {
        var registry = new SlotRegistry();
        registry.Register(new SlotDefinition("finger", "Finger", 2));
        registry.GetSlot("finger")!.Max.Should().Be(2);
    }

    [Fact]
    public void GetSlot_UnknownReturnsNull()
    {
        var registry = new SlotRegistry();
        registry.GetSlot("jetpack").Should().BeNull();
    }

    [Fact]
    public void AllSlots_ReturnsInOrder()
    {
        var registry = new SlotRegistry();
        registry.Register(new SlotDefinition("head", "Head", 1));
        registry.Register(new SlotDefinition("torso", "Torso", 1));
        registry.Register(new SlotDefinition("feet", "Feet", 1));
        registry.AllSlots.Should().HaveCount(3);
    }
}
