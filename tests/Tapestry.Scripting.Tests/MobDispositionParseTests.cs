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
}
