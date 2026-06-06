using Tapestry.Engine;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AreaDefinitionTests
{
    [Fact]
    public void EditableFields_AreMutableAfterConstruction()
    {
        var def = new AreaDefinition { Id = "road-to-tar-valon", Name = "Road" };

        def.Name = "Road to Tar Valon";
        def.Short = "A pilgrim road north.";
        def.Description = "The road stretches north.";
        def.Theme = "Grim pilgrimage under a watchful Tower.";
        def.Lore = "Built in the Age of Legends.";
        def.LevelRange = new[] { 5, 12 };
        def.ResetInterval = 300;
        def.SourcePack = "@mallek/legends-forgotten";

        Assert.Equal("Road to Tar Valon", def.Name);
        Assert.Equal("Grim pilgrimage under a watchful Tower.", def.Theme);
        Assert.Equal(new[] { 5, 12 }, def.LevelRange);
        Assert.Equal("@mallek/legends-forgotten", def.SourcePack);
    }

    [Fact]
    public void NewTextFields_DefaultToEmptyString()
    {
        var def = new AreaDefinition { Id = "x", Name = "X" };
        Assert.Equal("", def.Short);
        Assert.Equal("", def.Description);
        Assert.Equal("", def.Theme);
        Assert.Equal("", def.Lore);
        Assert.Null(def.SourcePack);
    }
}
