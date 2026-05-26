using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;

namespace Tapestry.Engine.Tests.Persistence;

public class AttributeWriterTests
{
    private readonly PropertyRegistry _props = new();
    private readonly TagRegistry _tags = new();
    private AttributeWriter Writer => new(_props, _tags);

    [Fact]
    public void Write_IntProperty_CoercesAndWrites()
    {
        _props.RegisterEngineProperty("hunger", "Hunger", PropertyValueType.Int, appliesTo: new[] { "player" });
        var target = new Entity("player", "Rocky");

        var result = Writer.Write(target, "hunger", new[] { "50" });

        Assert.True(result.Ok);
        Assert.Equal(50, target.GetProperty<int>("hunger"));
    }

    [Fact]
    public void Write_IntProperty_RejectsNonNumeric()
    {
        _props.RegisterEngineProperty("hunger", "Hunger", PropertyValueType.Int, appliesTo: new[] { "player" });
        var target = new Entity("player", "Rocky");

        var result = Writer.Write(target, "hunger", new[] { "abc" });

        Assert.False(result.Ok);
        Assert.Contains("whole number", result.Message);
    }

    [Fact]
    public void Write_StringProperty_JoinsTokens()
    {
        _props.RegisterEngineProperty("damage_dice", "Dice", PropertyValueType.String);
        var target = new Entity("item", "Sword");

        var result = Writer.Write(target, "damage_dice", new[] { "2d6+4" });

        Assert.True(result.Ok);
        Assert.Equal("2d6+4", target.GetProperty<string>("damage_dice"));
    }

    [Fact]
    public void Write_RejectsAttributeNotApplyingToTarget()
    {
        _props.RegisterEngineProperty("cookable", "Cookable", PropertyValueType.Bool, appliesTo: new[] { "item" });
        var target = new Entity("player", "Rocky");

        var result = Writer.Write(target, "cookable", new[] { "on" });

        Assert.False(result.Ok);
        Assert.Contains("applies to item", result.Message);
    }

    [Fact]
    public void Write_UndeclaredAttribute_Rejected()
    {
        var target = new Entity("player", "Rocky");

        var result = Writer.Write(target, "made_up", new[] { "1" });

        Assert.False(result.Ok);
        Assert.Contains("Unknown attribute", result.Message);
    }

    [Fact]
    public void Write_EnforcesMaxConstraint()
    {
        _props.RegisterEngineProperty("hunger", "Hunger", PropertyValueType.Int,
            appliesTo: new[] { "player" }, min: 0, max: 100);
        var target = new Entity("player", "Rocky");

        var result = Writer.Write(target, "hunger", new[] { "60000" });

        Assert.False(result.Ok);
        Assert.Contains("<= 100", result.Message);
    }

    [Fact]
    public void Write_RejectsCollectionTypeFromCommandLine()
    {
        _props.RegisterEngineProperty("keywords", "Keywords", PropertyValueType.ListString);
        var target = new Entity("item", "Sword");

        var result = Writer.Write(target, "keywords", new[] { "sharp" });

        Assert.False(result.Ok);
        Assert.Contains("command line", result.Message);
    }
}
