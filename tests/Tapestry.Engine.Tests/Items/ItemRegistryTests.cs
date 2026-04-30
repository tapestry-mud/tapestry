using FluentAssertions;
using Tapestry.Engine.Items;

namespace Tapestry.Engine.Tests.Items;

public class ItemRegistryTests
{
    private ItemTemplate CreateDaggerTemplate()
    {
        return new ItemTemplate
        {
            Id = "core:rusty-dagger",
            Name = "a rusty dagger",
            Type = "item:weapon",
            Tags = new List<string> { "item", "weapon" },
            Properties = new Dictionary<string, object?> { ["slot"] = "wield" },
            Modifiers = new List<ItemTemplate.ModifierEntry>
            {
                new() { Stat = "dexterity", Value = 1 }
            }
        };
    }

    [Fact]
    public void Register_StoresTemplate()
    {
        var registry = new ItemRegistry();

        registry.Register(CreateDaggerTemplate());

        registry.HasTemplate("core:rusty-dagger").Should().BeTrue();
        registry.Count.Should().Be(1);
    }

    [Fact]
    public void GetTemplate_ReturnsRegisteredTemplate()
    {
        var registry = new ItemRegistry();
        registry.Register(CreateDaggerTemplate());

        var template = registry.GetTemplate("core:rusty-dagger");

        template.Should().NotBeNull();
        template!.Name.Should().Be("a rusty dagger");
    }

    [Fact]
    public void GetTemplate_ReturnsNullForUnknown()
    {
        var registry = new ItemRegistry();

        registry.GetTemplate("core:nonexistent").Should().BeNull();
    }

    [Fact]
    public void HasTemplate_ReturnsFalseForUnknown()
    {
        var registry = new ItemRegistry();

        registry.HasTemplate("core:nonexistent").Should().BeFalse();
    }

    [Fact]
    public void CreateItem_ReturnsNewEntityFromTemplate()
    {
        var registry = new ItemRegistry();
        registry.Register(CreateDaggerTemplate());

        var item = registry.CreateItem("core:rusty-dagger");

        item.Should().NotBeNull();
        item!.Name.Should().Be("a rusty dagger");
        item.Type.Should().Be("item:weapon");
    }

    [Fact]
    public void CreateItem_ReturnsNullForUnknownTemplate()
    {
        var registry = new ItemRegistry();

        registry.CreateItem("core:nonexistent").Should().BeNull();
    }

    [Fact]
    public void CreateItem_ReturnsUniqueEntitiesEachCall()
    {
        var registry = new ItemRegistry();
        registry.Register(CreateDaggerTemplate());

        var item1 = registry.CreateItem("core:rusty-dagger");
        var item2 = registry.CreateItem("core:rusty-dagger");

        item1!.Id.Should().NotBe(item2!.Id);
    }

    [Fact]
    public void Register_OverwritesExistingTemplate()
    {
        var registry = new ItemRegistry();
        registry.Register(CreateDaggerTemplate());

        var updated = new ItemTemplate
        {
            Id = "core:rusty-dagger",
            Name = "a VERY rusty dagger",
            Type = "item:weapon",
            Tags = new List<string>(),
            Properties = new Dictionary<string, object?>(),
            Modifiers = new List<ItemTemplate.ModifierEntry>()
        };
        registry.Register(updated);

        registry.Count.Should().Be(1);
        registry.CreateItem("core:rusty-dagger")!.Name.Should().Be("a VERY rusty dagger");
    }
}
