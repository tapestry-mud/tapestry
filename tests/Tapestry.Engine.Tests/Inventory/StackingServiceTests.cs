using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;

namespace Tapestry.Engine.Tests.Inventory;

public class StackingServiceTests
{
    private static Entity MakeItem(string name, string? templateId, string? rarity = null, string? essence = null)
    {
        var item = new Entity("item", name);
        if (templateId != null) { item.SetProperty("template_id", templateId); }
        if (rarity != null) { item.SetProperty(ItemProperties.Rarity, rarity); }
        if (essence != null) { item.SetProperty(ItemProperties.Essence, essence); }
        return item;
    }

    private static Entity MakePlayer(params Entity[] items)
    {
        var player = new Entity("player", "Tester");
        foreach (var item in items) { player.AddToContents(item); }
        return player;
    }

    [Fact]
    public void ItemsWithSameTemplateId_StackTogether()
    {
        var service = new StackingService();
        var player = MakePlayer(
            MakeItem("a healing potion", "core:healing-potion"),
            MakeItem("a healing potion", "core:healing-potion")
        );
        var stacks = service.GetStacks(player);
        stacks.Should().HaveCount(1);
        stacks[0].Quantity.Should().Be(2);
    }

    [Fact]
    public void ItemWithoutTemplateId_NeverStacks()
    {
        var service = new StackingService();
        var player = MakePlayer(
            MakeItem("a handmade sword", null),
            MakeItem("a handmade sword", null)
        );
        var stacks = service.GetStacks(player);
        stacks.Should().HaveCount(2);
        stacks.Should().AllSatisfy(s => s.Quantity.Should().Be(1));
    }

    [Fact]
    public void ItemsWithDifferentEssence_DoNotStack()
    {
        var service = new StackingService();
        var player = MakePlayer(
            MakeItem("a rune blade", "core:rune-blade", essence: "fire"),
            MakeItem("a rune blade", "core:rune-blade", essence: "shadow")
        );
        var stacks = service.GetStacks(player);
        stacks.Should().HaveCount(2);
    }

    [Fact]
    public void ItemsWithSameEssence_StackTogether()
    {
        var service = new StackingService();
        var player = MakePlayer(
            MakeItem("a rune blade", "core:rune-blade", essence: "fire"),
            MakeItem("a rune blade", "core:rune-blade", essence: "fire")
        );
        var stacks = service.GetStacks(player);
        stacks.Should().HaveCount(1);
        stacks[0].Quantity.Should().Be(2);
        stacks[0].EssenceKey.Should().Be("fire");
    }

    [Fact]
    public void ItemsWithNullEssence_StackWithOtherNullEssence()
    {
        var service = new StackingService();
        var player = MakePlayer(
            MakeItem("a healing potion", "core:healing-potion"),
            MakeItem("a healing potion", "core:healing-potion")
        );
        var stacks = service.GetStacks(player);
        stacks.Should().HaveCount(1);
        stacks[0].EssenceKey.Should().BeNull();
    }

    [Fact]
    public void AddKey_CausesItemsWithDifferentPropertyToNotStack()
    {
        var service = new StackingService();
        service.AddKey("quality");
        var item1 = MakeItem("a sword", "core:sword");
        item1.SetProperty("quality", "masterwork");
        var item2 = MakeItem("a sword", "core:sword");
        item2.SetProperty("quality", "crude");
        var player = MakePlayer(item1, item2);
        var stacks = service.GetStacks(player);
        stacks.Should().HaveCount(2);
    }

    [Fact]
    public void StackOrder_PreservesBagOrder()
    {
        var service = new StackingService();
        var player = MakePlayer(
            MakeItem("a scroll", "core:scroll"),
            MakeItem("a potion", "core:potion"),
            MakeItem("a scroll", "core:scroll")
        );
        var stacks = service.GetStacks(player);
        stacks.Should().HaveCount(2);
        stacks[0].Name.Should().Be("a scroll");
        stacks[1].Name.Should().Be("a potion");
    }

    [Fact]
    public void StackEntry_ItemIds_ContainsAllStackedItemIds()
    {
        var service = new StackingService();
        var item1 = MakeItem("a potion", "core:potion");
        var item2 = MakeItem("a potion", "core:potion");
        var player = MakePlayer(item1, item2);
        var stacks = service.GetStacks(player);
        stacks[0].ItemIds.Should().HaveCount(2);
        stacks[0].ItemIds.Should().Contain(item1.Id.ToString());
        stacks[0].ItemIds.Should().Contain(item2.Id.ToString());
    }

    [Fact]
    public void EmptyInventory_ReturnsEmptyList()
    {
        var service = new StackingService();
        var player = new Entity("player", "Empty");
        service.GetStacks(player).Should().BeEmpty();
    }
}
