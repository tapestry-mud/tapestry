using FluentAssertions;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Persistence;

public class PlayerSerializerTests
{
    private readonly PropertyTypeRegistry _registry;
    private readonly PlayerSerializer _serializer;

    public PlayerSerializerTests()
    {
        _registry = new PropertyTypeRegistry();
        CommonProperties.Register(_registry);
        _serializer = new PlayerSerializer(_registry);
    }

    private Entity CreateTestPlayer()
    {
        var player = new Entity("player", "Krakus");
        player.LocationRoomId = "limbo:recall";
        return player;
    }

    [Fact]
    public void RoundTrip_PreservesBasicFields()
    {
        var player = CreateTestPlayer();
        var dto = _serializer.ToSaveData(player, "hash123", new List<Entity>(), new List<(Entity, List<Entity>)>());

        dto.Id.Should().Be(player.Id.ToString());
        dto.Name.Should().Be("Krakus");
        dto.Type.Should().Be("player");
        dto.PasswordHash.Should().Be("hash123");

        var result = _serializer.FromSaveData(dto);
        result.Entity.Id.Should().Be(player.Id);
        result.Entity.Name.Should().Be("Krakus");
        result.Entity.Type.Should().Be("player");
        result.PasswordHash.Should().Be("hash123");
    }

    [Fact]
    public void RoundTrip_PreservesStats()
    {
        var player = CreateTestPlayer();
        player.Stats.BaseStrength = 18;
        player.Stats.BaseMaxHp = 100;
        player.Stats.Hp = 75;

        var dto = _serializer.ToSaveData(player, "h", new List<Entity>(), new List<(Entity, List<Entity>)>());
        var result = _serializer.FromSaveData(dto);

        result.Entity.Stats.BaseStrength.Should().Be(18);
        result.Entity.Stats.BaseMaxHp.Should().Be(100);
        result.Entity.Stats.Hp.Should().Be(75);
    }

    [Fact]
    public void RoundTrip_PreservesStatModifiers()
    {
        var player = CreateTestPlayer();
        player.Stats.BaseMaxHp = 100;
        player.Stats.Hp = 100;
        player.Stats.AddModifier(new StatModifier("ring_of_power", StatType.Strength, 5));
        player.Stats.AddModifier(new StatModifier("blessing", StatType.MaxHp, 20));

        var dto = _serializer.ToSaveData(player, "h", new List<Entity>(), new List<(Entity, List<Entity>)>());
        var result = _serializer.FromSaveData(dto);

        result.Entity.Stats.Modifiers.Should().HaveCount(2);
        result.Entity.Stats.Modifiers[0].Source.Should().Be("ring_of_power");
        result.Entity.Stats.Modifiers[0].Stat.Should().Be(StatType.Strength);
        result.Entity.Stats.Modifiers[0].Value.Should().Be(5);
        result.Entity.Stats.Modifiers[1].Source.Should().Be("blessing");
        result.Entity.Stats.Modifiers[1].Stat.Should().Be(StatType.MaxHp);
        result.Entity.Stats.Modifiers[1].Value.Should().Be(20);
    }

    [Fact]
    public void RoundTrip_PreservesProperties()
    {
        var player = CreateTestPlayer();
        player.SetProperty(CommonProperties.RegenHp, 5);
        player.SetProperty(CommonProperties.TemplateId, "warrior_base");

        var dto = _serializer.ToSaveData(player, "h", new List<Entity>(), new List<(Entity, List<Entity>)>());
        var result = _serializer.FromSaveData(dto);

        result.Entity.GetProperty<int>(CommonProperties.RegenHp).Should().Be(5);
        result.Entity.GetProperty<string>(CommonProperties.TemplateId).Should().Be("warrior_base");
    }

    [Fact]
    public void RoundTrip_PreservesTags()
    {
        var player = CreateTestPlayer();
        player.AddTag("player");
        player.AddTag("admin");

        var dto = _serializer.ToSaveData(player, "h", new List<Entity>(), new List<(Entity, List<Entity>)>());
        var result = _serializer.FromSaveData(dto);

        result.Entity.Tags.Should().Contain("player");
        result.Entity.Tags.Should().Contain("admin");
    }

    [Fact]
    public void RoundTrip_PreservesInventoryItems()
    {
        var player = CreateTestPlayer();
        var sword = new Entity("item", "Iron Sword");
        sword.SetProperty(CommonProperties.TemplateId, "iron_sword");
        player.AddToContents(sword);

        var allItems = new List<Entity> { sword };
        var dto = _serializer.ToSaveData(player, "h", allItems, new List<(Entity, List<Entity>)>());
        var result = _serializer.FromSaveData(dto);

        result.Entity.Contents.Should().HaveCount(1);
        result.Entity.Contents[0].Id.Should().Be(sword.Id);
        result.Entity.Contents[0].Name.Should().Be("Iron Sword");
        result.Entity.Contents[0].GetProperty<string>(CommonProperties.TemplateId).Should().Be("iron_sword");
    }

    [Fact]
    public void RoundTrip_PreservesEquipment()
    {
        var player = CreateTestPlayer();
        var weapon = new Entity("item", "Steel Axe");
        player.AddToContents(weapon);
        player.SetEquipment("weapon", weapon);

        var allItems = new List<Entity> { weapon };
        var dto = _serializer.ToSaveData(player, "h", allItems, new List<(Entity, List<Entity>)>());
        var result = _serializer.FromSaveData(dto);

        result.Entity.Equipment.Should().ContainKey("weapon");
        result.Entity.Equipment["weapon"].Id.Should().Be(weapon.Id);
        result.Entity.Equipment["weapon"].Name.Should().Be("Steel Axe");
    }

    [Fact]
    public void RoundTrip_PreservesContainerRelationships()
    {
        var player = CreateTestPlayer();
        var bag = new Entity("item", "Leather Bag");
        bag.AddTag("container");
        var potion = new Entity("item", "Health Potion");
        bag.AddToContents(potion);
        player.AddToContents(bag);

        var allItems = new List<Entity> { bag, potion };
        var dto = _serializer.ToSaveData(player, "h", allItems, new List<(Entity, List<Entity>)>());
        var result = _serializer.FromSaveData(dto);

        result.Entity.Contents.Should().HaveCount(1);
        var restoredBag = result.Entity.Contents[0];
        restoredBag.Name.Should().Be("Leather Bag");
        restoredBag.Contents.Should().HaveCount(1);
        restoredBag.Contents[0].Name.Should().Be("Health Potion");
        restoredBag.Contents[0].Id.Should().Be(potion.Id);
    }

    [Fact]
    public void RoundTrip_PreservesCorpses()
    {
        var player = CreateTestPlayer();
        var corpse = new Entity("corpse", "corpse of Goblin");
        corpse.LocationRoomId = "forest:clearing";
        corpse.AddTag("corpse");
        corpse.SetProperty(CommonProperties.CorpseDecay, 10);
        var loot = new Entity("item", "Gold Coin");
        corpse.AddToContents(loot);

        var corpses = new List<(Entity Corpse, List<Entity> Items)>
        {
            (corpse, new List<Entity> { loot })
        };

        var dto = _serializer.ToSaveData(player, "h", new List<Entity>(), corpses);
        var result = _serializer.FromSaveData(dto);

        result.Corpses.Should().HaveCount(1);
        var (restoredCorpse, restoredItems) = result.Corpses[0];
        restoredCorpse.Id.Should().Be(corpse.Id);
        restoredCorpse.Name.Should().Be("corpse of Goblin");
        restoredCorpse.LocationRoomId.Should().Be("forest:clearing");
        restoredCorpse.Tags.Should().Contain("corpse");
        restoredCorpse.GetProperty<int>(CommonProperties.CorpseDecay).Should().Be(10);
        restoredItems.Should().HaveCount(1);
        restoredItems[0].Name.Should().Be("Gold Coin");
    }

    [Fact]
    public void RoundTrip_EmptyInventory_NoEquipment()
    {
        var player = CreateTestPlayer();

        var dto = _serializer.ToSaveData(player, "h", new List<Entity>(), new List<(Entity, List<Entity>)>());
        var result = _serializer.FromSaveData(dto);

        result.Entity.Contents.Should().BeEmpty();
        result.Entity.Equipment.Should().BeEmpty();
        result.AllItems.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_UnknownProperty_UsesTaggedFormat()
    {
        var player = CreateTestPlayer();
        player.SetProperty("custom_flag", true);

        var dto = _serializer.ToSaveData(player, "h", new List<Entity>(), new List<(Entity, List<Entity>)>());

        // Unknown property should be serialized as tagged dict
        dto.Properties["custom_flag"].Should().BeOfType<Dictionary<string, object?>>();
        var tagged = (Dictionary<string, object?>)dto.Properties["custom_flag"]!;
        tagged["type"].Should().Be("bool");
        tagged["value"].Should().Be(true);

        var result = _serializer.FromSaveData(dto);
        result.Entity.GetProperty<bool>("custom_flag").Should().BeTrue();
    }

    [Fact]
    public void Deserialize_BadValueForKnownProperty_UsesDefault()
    {
        var dto = new PlayerSaveData
        {
            Id = Guid.NewGuid().ToString(),
            Name = "TestPlayer",
            Type = "player",
            Location = "limbo:recall",
            Properties = new Dictionary<string, object?>
            {
                [CommonProperties.RegenHp] = "not_a_number"
            }
        };

        var result = _serializer.FromSaveData(dto);
        result.Entity.GetProperty<int>(CommonProperties.RegenHp).Should().Be(0);
    }

    [Fact]
    public void Deserialize_MissingProperties_DefaultGracefully()
    {
        var dto = new PlayerSaveData
        {
            Id = Guid.NewGuid().ToString(),
            Name = "TestPlayer",
            Type = "player",
            Location = "limbo:recall",
            Properties = new Dictionary<string, object?>()
        };

        var result = _serializer.FromSaveData(dto);
        result.Entity.Name.Should().Be("TestPlayer");
        result.Entity.LocationRoomId.Should().Be("limbo:recall");
    }

    [Fact]
    public void Deserialize_MissingCorpsesSection_ReturnsEmptyList()
    {
        var dto = new PlayerSaveData
        {
            Id = Guid.NewGuid().ToString(),
            Name = "TestPlayer",
            Type = "player",
            Location = "limbo:recall",
            Corpses = null!
        };

        var result = _serializer.FromSaveData(dto);
        result.Corpses.Should().BeEmpty();
    }
}
