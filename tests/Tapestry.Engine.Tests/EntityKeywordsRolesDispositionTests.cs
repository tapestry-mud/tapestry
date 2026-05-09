using FluentAssertions;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;

namespace Tapestry.Engine.Tests;

public class EntityKeywordsRolesDispositionTests
{
    [Fact]
    public void Keywords_DefaultsToEmpty()
    {
        var entity = new Entity("item", "a sword");
        entity.Keywords.Should().BeEmpty();
    }

    [Fact]
    public void AddKeyword_AddsToCollection()
    {
        var entity = new Entity("item", "a sword");
        entity.AddKeyword("blade");
        entity.AddKeyword("weapon");

        entity.Keywords.Should().Contain("blade");
        entity.Keywords.Should().Contain("weapon");
    }

    [Fact]
    public void HasKeyword_IsCaseInsensitive()
    {
        var entity = new Entity("item", "a sword");
        entity.AddKeyword("Blade");

        entity.HasKeyword("blade").Should().BeTrue();
        entity.HasKeyword("BLADE").Should().BeTrue();
    }

    [Fact]
    public void Roles_DefaultsToEmpty()
    {
        var entity = new Entity("player", "Travis");
        entity.Roles.Should().BeEmpty();
    }

    [Fact]
    public void AddRole_AddsToCollection()
    {
        var entity = new Entity("player", "Travis");
        entity.AddRole("admin");

        entity.Roles.Should().Contain("admin");
    }

    [Fact]
    public void HasRole_IsCaseInsensitive()
    {
        var entity = new Entity("player", "Travis");
        entity.AddRole("Admin");

        entity.HasRole("admin").Should().BeTrue();
    }

    [Fact]
    public void RemoveRole_RemovesFromCollection()
    {
        var entity = new Entity("player", "Travis");
        entity.AddRole("admin");
        entity.RemoveRole("admin");

        entity.HasRole("admin").Should().BeFalse();
    }

    [Fact]
    public void Disposition_DefaultsToNeutral()
    {
        var entity = new Entity("npc", "a guard");
        entity.Disposition.Should().Be(Disposition.Neutral);
    }

    [Fact]
    public void Disposition_CanBeSet()
    {
        var entity = new Entity("npc", "a goblin");
        entity.Disposition = Disposition.Hostile;

        entity.Disposition.Should().Be(Disposition.Hostile);
    }

    [Fact]
    public void ItemTemplate_CreateEntity_AppliesKeywords()
    {
        var template = new ItemTemplate
        {
            Id = "test:sword",
            Name = "a rusty sword",
            Type = "item",
            Tags = ["equippable"],
            Keywords = ["sword", "rusty", "blade"]
        };

        var entity = template.CreateEntity();

        entity.HasKeyword("sword").Should().BeTrue();
        entity.HasKeyword("blade").Should().BeTrue();
        entity.HasTag("equippable").Should().BeTrue();
        entity.HasKeyword("equippable").Should().BeFalse();
    }

    [Fact]
    public void MobTemplate_CreateEntity_AppliesKeywordsAndDisposition()
    {
        var template = new MobTemplate
        {
            Id = "test:goblin",
            Name = "a goblin",
            Type = "mob",
            Tags = ["killable"],
            Keywords = ["goblin", "creature"],
            BaseDisposition = Disposition.Hostile
        };

        var entity = template.CreateEntity();

        entity.HasKeyword("goblin").Should().BeTrue();
        entity.HasKeyword("creature").Should().BeTrue();
        entity.Disposition.Should().Be(Disposition.Hostile);
        entity.HasTag("killable").Should().BeTrue();
    }

    [Fact]
    public void MobTemplate_Disposition_DefaultsToNeutral()
    {
        var template = new MobTemplate
        {
            Id = "test:guard",
            Name = "a guard",
            Type = "npc"
        };

        var entity = template.CreateEntity();

        entity.Disposition.Should().Be(Disposition.Neutral);
    }

    [Fact]
    public void KeywordMatcher_MatchesEntityByKeyword()
    {
        var entity = new Entity("item", "a rusty iron dagger");
        entity.AddKeyword("dagger");
        entity.AddKeyword("knife");
        entity.AddKeyword("blade");

        entity.HasKeyword("dagger").Should().BeTrue();
        entity.HasKeyword("knife").Should().BeTrue();
        entity.HasKeyword("blade").Should().BeTrue();
        entity.HasKeyword("sword").Should().BeFalse();
    }

    [Fact]
    public void Keywords_DoNotAffectTags()
    {
        var entity = new Entity("item", "a rusty iron dagger");
        entity.AddKeyword("dagger");
        entity.AddTag("equippable");

        entity.HasTag("dagger").Should().BeFalse();
        entity.HasKeyword("equippable").Should().BeFalse();
    }
}
