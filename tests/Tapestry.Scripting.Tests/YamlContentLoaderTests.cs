using FluentAssertions;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests;

public class YamlContentLoaderTests
{
    [Fact]
    public void LoadRoom_ParsesBareYaml()
    {
        var yaml = """
            id: "core:test-room"
            area: test-area
            name: "Test Room"
            description: "A room."
            exits:
              north: "core:other-room"
            tags: [safe]
            properties:
              terrain: indoors
            """;

        var result = YamlContentLoader.LoadRoom(yaml);

        result.Room.Id.Should().Be("core:test-room");
        result.Room.Name.Should().Be("Test Room");
        result.Room.Area.Should().Be("test-area");
        result.Spawns.Should().BeEmpty();
        result.Fixtures.Should().BeEmpty();
        result.ResetInterval.Should().BeNull();
    }

    [Fact]
    public void LoadRoom_ParsesSpawnsAndFixtures()
    {
        var yaml = """
            id: "core:town-square"
            area: starter-town
            name: "Town Square"
            description: "A square."
            exits: {}
            reset_interval: 120
            spawns:
              - mob: "core:town-guard"
                count: 1
                tags: [persistent]
            fixtures:
              - "core:fountain"
              - "core:town-sign"
            """;

        var result = YamlContentLoader.LoadRoom(yaml);

        result.Spawns.Should().HaveCount(1);
        result.Spawns[0].Mob.Should().Be("core:town-guard");
        result.Spawns[0].Count.Should().Be(1);
        result.Spawns[0].Tags.Should().Contain("persistent");
        result.Fixtures.Should().Equal("core:fountain", "core:town-sign");
        result.ResetInterval.Should().Be(120);
    }

    [Fact]
    public void LoadItem_ParsesBareYaml()
    {
        var yaml = """
            id: "core:iron-sword"
            name: "an iron sword"
            type: "item:weapon"
            tags: [item, weapon]
            properties:
              slot: wield
              weight: 5
            modifiers:
              - stat: strength
                value: 2
            """;

        var result = YamlContentLoader.LoadItem(yaml);

        result.Id.Should().Be("core:iron-sword");
        result.Name.Should().Be("an iron sword");
        result.Properties["slot"].Should().Be("wield");
        result.Modifiers.Should().HaveCount(1);
        result.Modifiers[0].Stat.Should().Be("strength");
    }

    [Fact]
    public void LoadMob_ParsesBareYaml_NoLoot()
    {
        var yaml = """
            id: "core:test-dummy"
            name: "a training dummy"
            type: "npc"
            tags: [npc, mob, killable]
            behavior: stationary
            stats:
              strength: 1
              dexterity: 1
              constitution: 1
              intelligence: 1
              wisdom: 1
              luck: 1
              max_hp: 99999
              max_resource: 0
              max_movement: 0
            properties:
              level: 1
            equipment: []
            """;

        var (template, lootTable) = YamlContentLoader.LoadMob(yaml);

        template.Id.Should().Be("core:test-dummy");
        template.LootTable.Should().BeNull();
        lootTable.Should().BeNull();
    }

    [Fact]
    public void LoadMob_ParsesBareYaml_WithInlineLoot()
    {
        var yaml = """
            id: "core:goblin"
            name: "a goblin"
            type: "npc"
            tags: [npc, mob, hostile, killable]
            behavior: wander
            stats:
              strength: 8
              dexterity: 12
              constitution: 8
              intelligence: 4
              wisdom: 4
              luck: 6
              max_hp: 40
              max_resource: 0
              max_movement: 80
            properties:
              level: 3
            equipment: []
            loot:
              guaranteed:
                - item: "core:goblin-ear"
                  count: 1
              pool:
                - item: "core:rusty-dagger"
                  weight: 50
              pool_rolls: 1
              rare_bonus:
                chance: 0.05
                pool:
                  - item: "core:goblin-charm"
                    weight: 70
            """;

        var (template, lootTable) = YamlContentLoader.LoadMob(yaml);

        template.Id.Should().Be("core:goblin");
        template.LootTable.Should().Be("core:goblin");
        lootTable.Should().NotBeNull();
        lootTable!.Id.Should().Be("core:goblin");
        lootTable.Guaranteed.Should().HaveCount(1);
        lootTable.Guaranteed[0].Item.Should().Be("core:goblin-ear");
        lootTable.Pool.Should().HaveCount(1);
        lootTable.Pool[0].Item.Should().Be("core:rusty-dagger");
        lootTable.RareBonus.Should().NotBeNull();
        lootTable.RareBonus!.Chance.Should().Be(0.05);
    }
}
