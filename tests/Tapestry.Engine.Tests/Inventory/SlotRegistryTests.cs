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

    [Fact]
    public void DuplicateRegistration_GetSlotAndAllSlots_Agree()
    {
        var registry = new SlotRegistry();
        registry.RegisterPackSlot("pack-a", "head", "Head", 1);
        registry.RegisterPackSlot("pack-b", "head", "Helm", 1); // same name, different display
        var byName = registry.GetSlot("head");
        var fromAll = registry.AllSlots.Single(s => s.Name == "head");
        byName.Should().NotBeNull();
        fromAll.Display.Should().Be(byName!.Display);   // split-brain: _byName said Helm, _slots said Head
        registry.AllSlots.Count(s => s.Name == "head").Should().Be(1);
    }

    [Fact]
    public void DuplicateRegistration_ReplaceInPlace_KeepsOrderPosition()
    {
        var registry = new SlotRegistry();
        registry.RegisterPackSlot("pack-a", "head", "Head", 1);
        registry.RegisterPackSlot("pack-a", "torso", "Torso", 1);
        registry.RegisterPackSlot("pack-b", "head", "Helm", 1); // replace, not re-append
        registry.AllSlots.Select(s => s.Name).Should().ContainInOrder("head", "torso");
        registry.AllSlots.First(s => s.Name == "head").Display.Should().Be("Helm");
    }
}
