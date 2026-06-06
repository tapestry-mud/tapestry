using Tapestry.Engine;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class YamlContentLoaderAreaTests
{
    [Fact]
    public void Serialize_Then_Load_RoundTripsTextFields()
    {
        var def = new AreaDefinition
        {
            Id = "road-to-tar-valon",
            Name = "Road to Tar Valon",
            Short = "A pilgrim road north.",
            Description = "The road stretches north toward the White Tower.",
            Theme = "Grim pilgrimage under a watchful Tower.",
            Lore = "Built in the Age of Legends.",
            LevelRange = new[] { 5, 12 },
            ResetInterval = 300,
            SourcePack = "@mallek/legends-forgotten"
        };

        var yaml = YamlContentLoader.SerializeAreaDefinition(def);
        var loaded = YamlContentLoader.LoadAreaDefinition(yaml);

        Assert.Equal("road-to-tar-valon", loaded.Id);
        Assert.Equal("Road to Tar Valon", loaded.Name);
        Assert.Equal("A pilgrim road north.", loaded.Short);
        Assert.Equal("The road stretches north toward the White Tower.", loaded.Description);
        Assert.Equal("Grim pilgrimage under a watchful Tower.", loaded.Theme);
        Assert.Equal("Built in the Age of Legends.", loaded.Lore);
        Assert.Equal(new[] { 5, 12 }, loaded.LevelRange);
        Assert.Equal(300, loaded.ResetInterval);
    }

    [Fact]
    public void Serialize_NeverEmitsSourcePack()
    {
        var def = new AreaDefinition { Id = "x", Name = "X", SourcePack = "@mallek/legends-forgotten" };
        var yaml = YamlContentLoader.SerializeAreaDefinition(def);
        Assert.DoesNotContain("source_pack", yaml);
        Assert.DoesNotContain("legends-forgotten", yaml);
    }

    [Fact]
    public void Serialize_OmitsEmptyTextFields()
    {
        var def = new AreaDefinition { Id = "x", Name = "X" }; // theme/lore/etc empty
        var yaml = YamlContentLoader.SerializeAreaDefinition(def);
        Assert.DoesNotContain("theme:", yaml);
        Assert.DoesNotContain("lore:", yaml);
    }

    [Fact]
    public void Serialize_WrapsInAreaEnvelope()
    {
        var def = new AreaDefinition { Id = "x", Name = "X" };
        var yaml = YamlContentLoader.SerializeAreaDefinition(def);
        Assert.StartsWith("area:", yaml.TrimStart());
    }
}
