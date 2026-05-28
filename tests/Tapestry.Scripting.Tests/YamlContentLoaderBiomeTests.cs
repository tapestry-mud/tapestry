using FluentAssertions;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests;

public class YamlContentLoaderBiomeTests
{
    // Builds a TagRegistry with @tapestry/biomes:forest (kind: biome) and
    // @tapestry/core:safe (no kind), mimicking real pack registrations.
    // Rooms in these tests use the 'tapestry-example-pack' namespace, so a
    // dependency resolver is wired up to let bare 'forest'/'safe' resolve to
    // the registering packs — exactly how the engine resolves cross-pack tags.
    private static TagRegistry BuildTestRegistry()
    {
        var r = new TagRegistry();
        r.RegisterPackTag("tapestry-biomes", "forest", "Forest biome.", ["room"], kind: "biome");
        r.RegisterPackTag("tapestry-core", "safe", "Safe zone.", ["room"]);
        r.SetDependencyResolver(_ => ["tapestry-biomes", "tapestry-core"]);
        return r;
    }

    [Fact]
    public void LoadRoom_BiomeFieldDesugarsToTag()
    {
        var registry = BuildTestRegistry();
        var yaml = """
            id: "tapestry-example-pack:forest-glade"
            area: test
            name: "Forest Glade"
            description: "Trees."
            biome: forest
            """;

        var result = YamlContentLoader.LoadRoom(yaml, tagRegistry: registry);

        result.Room.HasTag("forest").Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void LoadRoom_BiomeFieldDoesNotDuplicateTagWhenAlsoInTagsList()
    {
        // biome: forest + tags: [forest] -> still one forest tag, but a warning
        var registry = BuildTestRegistry();
        var yaml = """
            id: "tapestry-example-pack:forest-glade"
            area: test
            name: "Forest Glade"
            description: "Trees."
            biome: forest
            tags: [forest]
            """;

        var result = YamlContentLoader.LoadRoom(yaml, tagRegistry: registry);

        result.Room.HasTag("forest").Should().BeTrue();
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Should().Contain("forest").And.Contain("biome");
    }

    [Fact]
    public void LoadRoom_BiomeTagInTagsListWithoutBiomeFieldWarns()
    {
        var registry = BuildTestRegistry();
        var yaml = """
            id: "tapestry-example-pack:forest-glade"
            area: test
            name: "Forest Glade"
            description: "Trees."
            tags: [forest]
            """;

        var result = YamlContentLoader.LoadRoom(yaml, tagRegistry: registry);

        result.Room.HasTag("forest").Should().BeTrue();
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Should().Contain("forest").And.Contain("biome");
    }

    [Fact]
    public void LoadRoom_NonBiomeTagsInTagsListNoWarning()
    {
        var registry = BuildTestRegistry();
        var yaml = """
            id: "tapestry-example-pack:town-square"
            area: test
            name: "Town Square"
            description: "A square."
            tags: [safe]
            """;

        var result = YamlContentLoader.LoadRoom(yaml, tagRegistry: registry);

        result.Room.HasTag("safe").Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void LoadRoom_BiomeFieldThrowsForNonBiomeTag()
    {
        var registry = BuildTestRegistry();
        var yaml = """
            id: "tapestry-example-pack:bad-room"
            area: test
            name: "Bad Room"
            description: "Oops."
            biome: safe
            """;

        var act = () => YamlContentLoader.LoadRoom(yaml, tagRegistry: registry);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*safe*not a biome*");
    }

    [Fact]
    public void LoadRoom_BiomeFieldThrowsForUnknownTag()
    {
        var registry = BuildTestRegistry();
        var yaml = """
            id: "tapestry-example-pack:bad-room"
            area: test
            name: "Bad Room"
            description: "Oops."
            biome: swamp
            """;

        var act = () => YamlContentLoader.LoadRoom(yaml, tagRegistry: registry);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*swamp*not a known*");
    }

    [Fact]
    public void LoadRoom_BiomeFieldSkipsValidationWhenNoRegistryProvided()
    {
        // Without a registry we can't validate — just desugar. Existing callers
        // (unit tests, one-off loads) that don't pass a registry must still work.
        var yaml = """
            id: "tapestry-example-pack:forest-glade"
            area: test
            name: "Forest Glade"
            description: "Trees."
            biome: forest
            """;

        var result = YamlContentLoader.LoadRoom(yaml);

        result.Room.HasTag("forest").Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }
}
