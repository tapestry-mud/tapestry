using FluentAssertions;
using Tapestry.Engine.Items;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Items;

public class ItemTemplateTests
{
    private ItemTemplate CreateDaggerTemplate()
    {
        return new ItemTemplate
        {
            Id = "core:rusty-dagger",
            Name = "a rusty dagger",
            Type = "item:weapon",
            Tags = new List<string> { "item", "weapon", "dagger", "rusty" },
            Properties = new Dictionary<string, object?>
            {
                ["slot"] = "wield",
                ["weight"] = 2,
                ["rarity"] = "common"
            },
            Modifiers = new List<ItemTemplate.ModifierEntry>
            {
                new() { Stat = "dexterity", Value = 1 }
            }
        };
    }

    [Fact]
    public void CreateEntity_SetsTypeAndName()
    {
        var template = CreateDaggerTemplate();

        var entity = template.CreateEntity();

        entity.Type.Should().Be("item:weapon");
        entity.Name.Should().Be("a rusty dagger");
    }

    [Fact]
    public void CreateEntity_CopiesTags()
    {
        var template = CreateDaggerTemplate();

        var entity = template.CreateEntity();

        entity.HasTag("item").Should().BeTrue();
        entity.HasTag("weapon").Should().BeTrue();
        entity.HasTag("dagger").Should().BeTrue();
        entity.HasTag("rusty").Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_CopiesProperties()
    {
        var template = CreateDaggerTemplate();

        var entity = template.CreateEntity();

        entity.GetProperty<string>("slot").Should().Be("wield");
        entity.GetProperty<string>("rarity").Should().Be("common");
    }

    [Fact]
    public void CreateEntity_SetsTemplateId()
    {
        var template = CreateDaggerTemplate();

        var entity = template.CreateEntity();

        entity.GetProperty<string>("template_id").Should().Be("core:rusty-dagger");
    }

    [Fact]
    public void CreateEntity_CreatesStatModifiers()
    {
        var template = CreateDaggerTemplate();

        var entity = template.CreateEntity();

        var modifiers = entity.GetProperty<List<StatModifier>>("modifiers");
        modifiers.Should().NotBeNull();
        modifiers.Should().HaveCount(1);
        modifiers![0].Stat.Should().Be(StatType.Dexterity);
        modifiers[0].Value.Should().Be(1);
        modifiers[0].Source.Should().StartWith("equipment:");
    }

    [Fact]
    public void CreateEntity_ReturnsUniqueEntitiesEachCall()
    {
        var template = CreateDaggerTemplate();

        var entity1 = template.CreateEntity();
        var entity2 = template.CreateEntity();

        entity1.Id.Should().NotBe(entity2.Id);
    }

    [Fact]
    public void CreateEntity_WithNoModifiers_SetsEmptyList()
    {
        var template = new ItemTemplate
        {
            Id = "core:goblin-ear",
            Name = "a goblin ear",
            Type = "item:junk",
            Tags = new List<string> { "item", "junk" },
            Properties = new Dictionary<string, object?> { ["weight"] = 0 },
            Modifiers = new List<ItemTemplate.ModifierEntry>()
        };

        var entity = template.CreateEntity();

        var modifiers = entity.GetProperty<List<StatModifier>>("modifiers");
        modifiers.Should().NotBeNull();
        modifiers.Should().BeEmpty();
    }
}
