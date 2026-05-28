using FluentAssertions;
using Tapestry.Engine.Distribution;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Distribution;

public class SpawnOnParseTests
{
    private const string BaseItemYaml = """
        id: "test-pack:copper-chunk"
        name: "a chunk of copper"
        type: "item"
        keywords: [copper, chunk]
        properties:
          material: copper
          weight: 2
          value: 12
        """;

    // --- LoadItem round-trip tests ---

    [Fact]
    public void LoadItem_WithNoSpawnOn_ReturnsNullSpawnOn()
    {
        var def = YamlContentLoader.LoadItem(BaseItemYaml);

        def.SpawnOn.Should().BeNull();
    }

    [Fact]
    public void LoadItem_WithTagEntry_ParsesRawModel()
    {
        var yaml = BaseItemYaml + """

        spawn_on:
          - tag: cave-dweller
            chance: 0.15
            count: 2
        """;

        var def = YamlContentLoader.LoadItem(yaml);

        def.SpawnOn.Should().HaveCount(1);
        var entry = def.SpawnOn![0];
        entry.Tag.Should().Be("cave-dweller");
        entry.Id.Should().BeNull();
        entry.Type.Should().BeNull();
        entry.Chance.Should().BeApproximately(0.15, 0.00001);
        entry.Count.Should().Be(2);
    }

    [Fact]
    public void LoadItem_WithTagEntry_NoChanceOrCount_UsesDefaults()
    {
        var yaml = BaseItemYaml + """

        spawn_on:
          - tag: surface-creature
        """;

        var def = YamlContentLoader.LoadItem(yaml);

        def.SpawnOn.Should().HaveCount(1);
        var entry = def.SpawnOn![0];
        entry.Tag.Should().Be("surface-creature");
        entry.Chance.Should().BeNull();
        entry.Count.Should().Be(1);
    }

    [Fact]
    public void LoadItem_WithRarity_RawModelHasRarityAndNullChance()
    {
        var yaml = BaseItemYaml + """

        spawn_on:
          - tag: beast
            rarity: rare
        """;

        var def = YamlContentLoader.LoadItem(yaml);

        def.SpawnOn.Should().HaveCount(1);
        var entry = def.SpawnOn![0];
        entry.Rarity.Should().Be("rare");
        entry.Chance.Should().BeNull();
    }

    // --- ParseSpawnOnEntry helper tests ---

    [Fact]
    public void ParseSpawnOnEntry_WithTagAndRarity_ResolvesChanceFromRarity()
    {
        var model = new YamlContentLoader.SpawnOnEntryModel
        {
            Tag = "mine",
            Rarity = "rare",
            Count = 1
        };

        var entry = YamlContentLoader.ParseSpawnOnEntry(model);

        entry.Chance.Should().BeApproximately(0.15, 0.00001);
        entry.Selector.Tag.Should().Be("mine");
        entry.Count.Should().Be(1);
    }

    [Fact]
    public void ParseSpawnOnEntry_WithShopAndRarity_ResolvesChanceAndSetsShop()
    {
        var model = new YamlContentLoader.SpawnOnEntryModel
        {
            Shop = true,
            Rarity = "rare"
        };

        var entry = YamlContentLoader.ParseSpawnOnEntry(model);

        entry.Selector.Shop.Should().BeTrue();
        entry.Chance.Should().BeApproximately(0.15, 0.00001);
    }

    [Fact]
    public void ParseSpawnOnEntry_WithBothRarityAndChance_ThrowsInvalidOperationException()
    {
        var model = new YamlContentLoader.SpawnOnEntryModel
        {
            Tag = "beast",
            Rarity = "rare",
            Chance = 0.10
        };

        var act = () => YamlContentLoader.ParseSpawnOnEntry(model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*rarity*chance*");
    }

    [Fact]
    public void ParseSpawnOnEntry_WithNoTargetingKey_ThrowsInvalidOperationException()
    {
        var model = new YamlContentLoader.SpawnOnEntryModel
        {
            Count = 2
        };

        var act = () => YamlContentLoader.ParseSpawnOnEntry(model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*targeting key*");
    }

    [Fact]
    public void ParseSpawnOnEntry_ScopeGlobal_ParsesGlobalScope()
    {
        var model = new YamlContentLoader.SpawnOnEntryModel
        {
            Tag = "forest_room",
            Chance = 1.0,
            Count = 3,
            Scope = "global"
        };

        var entry = YamlContentLoader.ParseSpawnOnEntry(model);

        entry.Scope.Should().Be(Tapestry.Engine.Distribution.SpawnScope.Global);
    }

    [Fact]
    public void ParseSpawnOnEntry_ScopeAbsent_DefaultsToRoomScope()
    {
        var model = new YamlContentLoader.SpawnOnEntryModel
        {
            Tag = "forest_room",
            Chance = 1.0,
            Count = 3
        };

        var entry = YamlContentLoader.ParseSpawnOnEntry(model);

        entry.Scope.Should().Be(Tapestry.Engine.Distribution.SpawnScope.Room);
    }

    [Fact]
    public void ParseSpawnOnEntry_ScopeRoom_ExplicitRoomScope()
    {
        var model = new YamlContentLoader.SpawnOnEntryModel
        {
            Tag = "forest_room",
            Chance = 1.0,
            Count = 3,
            Scope = "room"
        };

        var entry = YamlContentLoader.ParseSpawnOnEntry(model);

        entry.Scope.Should().Be(Tapestry.Engine.Distribution.SpawnScope.Room);
    }

    [Fact]
    public void ParseSpawnOnEntry_UnknownScope_ThrowsInvalidOperationException()
    {
        var model = new YamlContentLoader.SpawnOnEntryModel
        {
            Tag = "forest_room",
            Chance = 1.0,
            Count = 3,
            Scope = "biome"
        };

        var act = () => YamlContentLoader.ParseSpawnOnEntry(model);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unknown scope*biome*");
    }
}
