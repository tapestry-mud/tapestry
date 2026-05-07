using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests;

public class EntityTests
{
    [Fact]
    public void NewEntity_HasIdAndType()
    {
        var entity = new Entity("player", "Rand");
        entity.Id.Should().NotBeEmpty();
        entity.Type.Should().Be("player");
        entity.Name.Should().Be("Rand");
    }

    [Fact]
    public void Properties_SetAndGet()
    {
        var entity = new Entity("item:weapon", "Heron-Mark Blade");
        entity.SetProperty("damage", 15);
        entity.SetProperty("weight", 3.5);
        entity.GetProperty<int>("damage").Should().Be(15);
        entity.GetProperty<double>("weight").Should().Be(3.5);
    }

    [Fact]
    public void GetProperty_ReturnsDefault_WhenMissing()
    {
        var entity = new Entity("mob", "Elf");
        entity.GetProperty<int>("nonexistent").Should().Be(0);
        entity.GetProperty<string>("missing").Should().BeNull();
    }

    [Fact]
    public void Tags_AddCheckRemove()
    {
        var entity = new Entity("mob:hostile", "Myrddraal");
        entity.AddTag("hostile");
        entity.AddTag("shadowspawn");
        entity.HasTag("hostile").Should().BeTrue();
        entity.HasTag("friendly").Should().BeFalse();
        entity.RemoveTag("hostile");
        entity.HasTag("hostile").Should().BeFalse();
    }

    [Fact]
    public void Contents_AddAndRemove()
    {
        var player = new Entity("player", "Perrin");
        var axe = new Entity("item:weapon", "Battle Axe");
        player.AddToContents(axe);
        player.Contents.Should().Contain(axe);
        axe.Container.Should().Be(player);
        player.RemoveFromContents(axe);
        player.Contents.Should().NotContain(axe);
        axe.Container.Should().BeNull();
    }

    [Fact]
    public void Contents_AddSetsContainerAndClearsLocation()
    {
        var player = new Entity("player", "Mat");
        var coin = new Entity("item:currency", "Gold Crown");
        coin.LocationRoomId = "example-pack:town-square";
        player.AddToContents(coin);
        coin.Container.Should().Be(player);
        coin.LocationRoomId.Should().BeNull();
    }

    [Fact]
    public void Entity_HasStatBlock()
    {
        var entity = new Entity("player", "Rand");
        entity.Stats.Should().NotBeNull();
        entity.Stats.BaseStrength.Should().Be(0);
    }

    [Fact]
    public void Entity_Stats_ModifiersAffectEffective()
    {
        var entity = new Entity("player", "Rand");
        entity.Stats.BaseStrength = 20;
        entity.Stats.AddModifier(new StatModifier("buff:rage", StatType.Strength, 5));
        entity.Stats.Strength.Should().Be(25);
    }

    [Fact]
    public void Entity_AcceptsOptionalId()
    {
        var id = Guid.NewGuid();
        var entity = new Entity("player", "Krakus", id);
        entity.Id.Should().Be(id);
    }

    [Fact]
    public void Entity_GeneratesId_WhenNoneProvided()
    {
        var entity = new Entity("player", "Krakus");
        entity.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void TryGetProperty_ReturnsTrue_WhenKeyExists()
    {
        var entity = new Entity("player", "Rand");
        entity.SetProperty("damage", 15);
        var found = entity.TryGetProperty<int>("damage", out var value);
        found.Should().BeTrue();
        value.Should().Be(15);
    }

    [Fact]
    public void TryGetProperty_ReturnsFalse_WhenKeyMissing()
    {
        var entity = new Entity("player", "Rand");
        var found = entity.TryGetProperty<int>("nonexistent", out var value);
        found.Should().BeFalse();
        value.Should().Be(0);
    }

    [Fact]
    public void TryGetProperty_DistinguishesMissingFromNullValue()
    {
        // SetProperty with null removes the key
        var entity = new Entity("player", "Rand");
        entity.SetProperty("key", (object?)null);
        var found = entity.TryGetProperty<string>("key", out var value);
        found.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void EnumerateProperties_ReturnsOnlyMatchingPrefix()
    {
        var entity = new Entity("player", "Rand");
        entity.SetProperty("proficiency:kick", 42);
        entity.SetProperty("proficiency:dodge", 15);
        entity.SetProperty("cap:kick", 25);
        entity.SetProperty("other", "value");

        var matches = entity.EnumerateProperties("proficiency:").ToList();

        matches.Should().HaveCount(2);
        matches.Should().Contain(kv => kv.Key == "proficiency:kick" && (int)kv.Value! == 42);
        matches.Should().Contain(kv => kv.Key == "proficiency:dodge" && (int)kv.Value! == 15);
    }

    [Fact]
    public void EnumerateProperties_ReturnsEmpty_WhenNoPrefixMatch()
    {
        var entity = new Entity("player", "Rand");
        entity.SetProperty("cap:kick", 25);

        var matches = entity.EnumerateProperties("proficiency:").ToList();

        matches.Should().BeEmpty();
    }
}
