using FluentAssertions;
using Tapestry.Engine.Items;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests;

public class ItemContentsParseTests
{
    private const string BaseChestYaml = """
        id: "test-pack:oak-chest"
        name: "an oak chest"
        type: "container"
        """;

    [Fact]
    public void LoadItem_WithNoContents_ReturnsNullContents()
    {
        var def = YamlContentLoader.LoadItem(BaseChestYaml);
        def.Contents.Should().BeNull();
    }

    [Fact]
    public void ParseContents_Null_ReturnsEmptyList()
    {
        YamlContentLoader.ParseContents(null).Should().BeEmpty();
    }

    [Fact]
    public void ParseContents_BareString_IsLeafEntryWithCountOne()
    {
        var yaml = BaseChestYaml + """

        contents:
          - test-pack:health-potion
        """;
        var def = YamlContentLoader.LoadItem(yaml);

        var contents = YamlContentLoader.ParseContents(def.Contents);

        contents.Should().HaveCount(1);
        contents[0].TemplateId.Should().Be("test-pack:health-potion");
        contents[0].Count.Should().Be(1);
        contents[0].Contents.Should().BeEmpty();
    }

    [Fact]
    public void ParseContents_ObjectFormWithCount_ParsesCount()
    {
        var yaml = BaseChestYaml + """

        contents:
          - item: test-pack:gold-coin
            count: 10
        """;
        var def = YamlContentLoader.LoadItem(yaml);

        var contents = YamlContentLoader.ParseContents(def.Contents);

        contents.Should().HaveCount(1);
        contents[0].TemplateId.Should().Be("test-pack:gold-coin");
        contents[0].Count.Should().Be(10);
        contents[0].Contents.Should().BeEmpty();
    }

    [Fact]
    public void ParseContents_ObjectFormWithoutCount_DefaultsToOne()
    {
        var yaml = BaseChestYaml + """

        contents:
          - item: test-pack:locked-box
            contents:
              - test-pack:gold-ring
        """;
        var def = YamlContentLoader.LoadItem(yaml);

        var contents = YamlContentLoader.ParseContents(def.Contents);

        contents[0].Count.Should().Be(1);
    }

    [Fact]
    public void ParseContents_ObjectForm_ParsesNestedContents()
    {
        var yaml = BaseChestYaml + """

        contents:
          - item: test-pack:locked-box
            contents:
              - test-pack:gold-ring
        """;
        var def = YamlContentLoader.LoadItem(yaml);

        var contents = YamlContentLoader.ParseContents(def.Contents);

        contents.Should().HaveCount(1);
        contents[0].TemplateId.Should().Be("test-pack:locked-box");
        contents[0].Contents.Should().HaveCount(1);
        contents[0].Contents[0].TemplateId.Should().Be("test-pack:gold-ring");
        contents[0].Contents[0].Contents.Should().BeEmpty();
    }
}
