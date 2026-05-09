using FluentAssertions;

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
}
