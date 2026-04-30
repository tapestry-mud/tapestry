using FluentAssertions;
using Tapestry.Engine.Mobs;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Scripting.Tests;

public class MobDispositionParseTests
{
    private static Dictionary<string, List<MobTemplate>> DeserializeYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<Dictionary<string, List<MobTemplate>>>(yaml);
    }

    [Fact]
    public void MobWithDispositionBlock_SetsDispositionPropertyOnEntity()
    {
        var yaml = """
            mobs:
              - id: test:bandit
                name: a bandit
                type: npc
                disposition:
                  default: hostile
                  rules:
                    - when:
                        min_alignment: 500
                      reaction: friendly
            """;

        var doc = DeserializeYaml(yaml);
        doc["mobs"].Should().HaveCount(1);
        var template = doc["mobs"][0];
        var entity = template.CreateEntity();

        var disposition = entity.GetProperty<DispositionDefinition>("disposition");
        disposition.Should().NotBeNull();
    }

    [Fact]
    public void MobWithDispositionBlock_DefaultReactionIsParsedCorrectly()
    {
        var yaml = """
            mobs:
              - id: test:guard
                name: a guard
                type: npc
                disposition:
                  default: neutral
                  rules: []
            """;

        var doc = DeserializeYaml(yaml);
        var template = doc["mobs"][0];
        var entity = template.CreateEntity();

        var disposition = entity.GetProperty<DispositionDefinition>("disposition");
        disposition.Should().NotBeNull();
        disposition!.Default.Should().Be("neutral");
    }

    [Fact]
    public void MobWithDispositionBlock_RulesAreMappedCorrectly()
    {
        var yaml = """
            mobs:
              - id: test:priest
                name: a priest
                type: npc
                disposition:
                  default: neutral
                  rules:
                    - when:
                        min_alignment: 500
                      reaction: friendly
                    - when:
                        max_alignment: -500
                      reaction: hostile
            """;

        var doc = DeserializeYaml(yaml);
        var template = doc["mobs"][0];
        var entity = template.CreateEntity();

        var disposition = entity.GetProperty<DispositionDefinition>("disposition");
        disposition.Should().NotBeNull();
        disposition!.Rules.Should().HaveCount(2);

        var friendlyRule = disposition.Rules[0];
        friendlyRule.Reaction.Should().Be("friendly");
        friendlyRule.When.MinAlignment.Should().Be(500);
        friendlyRule.When.MaxAlignment.Should().BeNull();

        var hostileRule = disposition.Rules[1];
        hostileRule.Reaction.Should().Be("hostile");
        hostileRule.When.MaxAlignment.Should().Be(-500);
        hostileRule.When.MinAlignment.Should().BeNull();
    }

    [Fact]
    public void MobWithoutDispositionBlock_DoesNotSetDispositionProperty()
    {
        var yaml = """
            mobs:
              - id: test:rat
                name: a rat
                type: npc
            """;

        var doc = DeserializeYaml(yaml);
        var template = doc["mobs"][0];
        var entity = template.CreateEntity();

        var disposition = entity.GetProperty<DispositionDefinition>("disposition");
        disposition.Should().BeNull();
    }

    [Fact]
    public void MobWithDispositionBlock_BucketsAreMappedCorrectly()
    {
        var yaml = """
            mobs:
              - id: test:city-guard
                name: a city guard
                type: npc
                disposition:
                  default: neutral
                  rules:
                    - when:
                        buckets:
                          - good
                          - neutral
                      reaction: friendly
            """;

        var doc = DeserializeYaml(yaml);
        var template = doc["mobs"][0];
        var entity = template.CreateEntity();

        var disposition = entity.GetProperty<DispositionDefinition>("disposition");
        disposition.Should().NotBeNull();
        disposition!.Rules.Should().HaveCount(1);
        disposition.Rules[0].When.Buckets.Should().BeEquivalentTo(new[] { "good", "neutral" });
    }

    [Fact]
    public void MobWithIdleCommands_SetsPropertiesOnEntity()
    {
        var yaml = """
            mobs:
              - id: test:herald
                name: the herald
                type: npc
                idle_chance: 0.5
                idle_interval: 20
                idle_commands:
                  - 'say Hear ye!'
                  - 'emote rings a bell.'
            """;

        var doc = DeserializeYaml(yaml);
        var template = doc["mobs"][0];

        Assert.Equal(0.5, template.IdleChance);
        Assert.Equal(20, template.IdleInterval);
        Assert.Equal(2, template.IdleCommands.Count);
        Assert.Contains("say Hear ye!", template.IdleCommands);

        var entity = template.CreateEntity();
        var storedCommands = entity.GetProperty<List<string>>("idle_commands");
        Assert.NotNull(storedCommands);
        Assert.Equal(2, storedCommands!.Count);
    }

    [Fact]
    public void MobWithScript_SetsScriptPropertyOnEntity()
    {
        var yaml = """
            mobs:
              - id: test:guide
                name: the guide
                type: npc
                script: mobs/guide.js
            """;

        var doc = DeserializeYaml(yaml);
        var template = doc["mobs"][0];

        Assert.Equal("mobs/guide.js", template.Script);

        var entity = template.CreateEntity();
        Assert.Equal("mobs/guide.js", entity.GetProperty<string>("script"));
    }

    [Fact]
    public void MobWithoutIdleCommands_HasEmptyList_AndNoEntityProperty()
    {
        var yaml = """
            mobs:
              - id: test:simple
                name: a mob
                type: npc
            """;

        var doc = DeserializeYaml(yaml);
        var entity = doc["mobs"][0].CreateEntity();

        Assert.Null(entity.GetProperty<List<string>>("idle_commands"));
    }
}
