using Tapestry.Engine.Mobs;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class MobTemplateBattleCommandTests
{
    [Fact]
    public void MobTemplate_DeserializesAbilitiesAsStringList()
    {
        var yaml = """
            id: test:goblin
            name: a goblin
            type: npc
            abilities:
              - lf:fireball
              - lf:shield
            battle_commands:
              - fireball
              - shield
              - ""
            ability_proficiency: 85
            """;

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new MobAbilityEntryConverter())
            .Build();

        var template = deserializer.Deserialize<MobTemplate>(yaml);

        Assert.Equal(2, template.Abilities.Count);
        Assert.Equal("lf:fireball", template.Abilities[0].Id);
        Assert.Equal(3, template.BattleCommands.Count);
        Assert.Equal("", template.BattleCommands[2]);
        Assert.Equal(85, template.AbilityProficiency);
    }

    [Fact]
    public void MobTemplate_DeserializesStructuredAbilityEntries()
    {
        var yaml = """
            id: test:mage
            name: a mage
            type: npc
            abilities:
              - id: lf:fireball
                proficiency: 90
              - id: lf:shield
                proficiency: 70
            """;

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new MobAbilityEntryConverter())
            .Build();

        var template = deserializer.Deserialize<MobTemplate>(yaml);

        Assert.Equal(90, template.Abilities[0].Proficiency);
        Assert.Equal(70, template.Abilities[1].Proficiency);
    }

    [Fact]
    public void MobTemplate_CreateEntity_BattleCommandsOnTemplate()
    {
        var template = new MobTemplate
        {
            Id = "test:goblin",
            Name = "a goblin",
            Type = "npc",
            BattleCommands = new() { "fireball", "" }
        };

        var entity = template.CreateEntity();

        // BattleCommands live on the template, not the entity property bag.
        Assert.Equal(2, template.BattleCommands.Count);
        Assert.Equal("fireball", template.BattleCommands[0]);
        _ = entity; // entity creation should succeed without exception
    }
}
