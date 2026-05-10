using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;

namespace Tapestry.Engine.Tests.Tags;

public class TypeGraduationTests
{
    [Fact]
    public void ItemTemplate_CreateEntity_DoesNotAddTypeTag()
    {
        var template = new ItemTemplate
        {
            Id = "test:sword",
            Name = "a sword",
            Type = "item",
            Tags = ["item", "equippable"]
        };

        var entity = template.CreateEntity();

        entity.Type.Should().Be("item");
        entity.HasTag("item").Should().BeFalse("type tags should not be duplicated into the tag set");
        entity.HasTag("equippable").Should().BeTrue();
    }

    [Fact]
    public void ItemTemplate_CreateEntity_KeepsNonTypeTag_WhenTypeIsSubtype()
    {
        // Type is "item:weapon" but tag "item" is not the same string -- it stays
        var template = new ItemTemplate
        {
            Id = "test:longsword",
            Name = "a longsword",
            Type = "item:weapon",
            Tags = ["item", "equippable"]
        };

        var entity = template.CreateEntity();

        entity.Type.Should().Be("item:weapon");
        entity.HasTag("item:weapon").Should().BeFalse();
        entity.HasTag("item").Should().BeTrue();
        entity.HasTag("equippable").Should().BeTrue();
    }

    [Fact]
    public void MobTemplate_CreateEntity_DoesNotAddTypeTag()
    {
        var template = new MobTemplate
        {
            Id = "test:guard",
            Name = "a guard",
            Type = "npc",
            Tags = ["npc", "no_kill"]
        };

        var entity = template.CreateEntity();

        entity.Type.Should().Be("npc");
        entity.HasTag("npc").Should().BeFalse("type tags should not be duplicated into the tag set");
        entity.HasTag("no_kill").Should().BeTrue();
    }

    [Fact]
    public void Entity_Type_IdentifiesWithoutTag()
    {
        var player = new Entity("player", "Travis");
        var npc = new Entity("npc", "a guard");
        var item = new Entity("item", "a sword");

        player.Type.Should().Be("player");
        npc.Type.Should().Be("npc");
        item.Type.Should().Be("item");

        player.HasTag("player").Should().BeFalse();
        npc.HasTag("npc").Should().BeFalse();
        item.HasTag("item").Should().BeFalse();
    }

    [Fact]
    public void World_GetEntitiesByType_ReturnsMatchingEntities()
    {
        var world = new World();
        var player = new Entity("player", "Travis");
        var npc = new Entity("npc", "a guard");
        var item = new Entity("item", "a sword");

        world.TrackEntity(player);
        world.TrackEntity(npc);
        world.TrackEntity(item);

        var players = world.GetEntitiesByType("player").ToList();
        var npcs = world.GetEntitiesByType("npc").ToList();
        var items = world.GetEntitiesByType("item").ToList();

        players.Should().ContainSingle().Which.Should().BeSameAs(player);
        npcs.Should().ContainSingle().Which.Should().BeSameAs(npc);
        items.Should().ContainSingle().Which.Should().BeSameAs(item);
    }

    [Fact]
    public void World_GetEntitiesByType_ExcludesUntrackedEntities()
    {
        var world = new World();
        var player = new Entity("player", "Travis");
        world.TrackEntity(player);
        world.UntrackEntity(player);

        world.GetEntitiesByType("player").Should().BeEmpty();
    }
}
