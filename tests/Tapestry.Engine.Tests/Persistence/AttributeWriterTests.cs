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

    [Fact]
    public void Write_TagOn_AddsTag()
    {
        _tags.RegisterEngineTag("no_get", "Cannot be picked up", new[] { "item" });
        var target = new Entity("item", "Anvil");

        var result = Writer.Write(target, "no_get", new[] { "on" });

        Assert.True(result.Ok);
        Assert.True(target.HasTag("no_get"));
    }

    [Fact]
    public void Write_TagOff_RemovesTag()
    {
        _tags.RegisterEngineTag("no_get", "Cannot be picked up", new[] { "item" });
        var target = new Entity("item", "Anvil");
        target.AddTag("no_get");

        var result = Writer.Write(target, "no_get", new[] { "off" });

        Assert.True(result.Ok);
        Assert.False(target.HasTag("no_get"));
    }

    [Fact]
    public void Write_ResolvesPropertyBeforeTag_WhenNameCollides()
    {
        // Same bare name declared as both a property and a tag: property wins.
        _props.RegisterEngineProperty("shiny", "Shininess", PropertyValueType.Int, appliesTo: new[] { "item" });
        _tags.RegisterEngineTag("shiny", "Is shiny", new[] { "item" });
        var target = new Entity("item", "Coin");

        var result = Writer.Write(target, "shiny", new[] { "7" });

        Assert.True(result.Ok);
        Assert.Equal(7, target.GetProperty<int>("shiny"));
        Assert.False(target.HasTag("shiny"));
    }

    [Fact]
    public void ValueTypeName_MapsEnumToSnakeStrings()
    {
        Assert.Equal("int", AttributeWriter.ValueTypeName(PropertyValueType.Int));
        Assert.Equal("map_int", AttributeWriter.ValueTypeName(PropertyValueType.MapInt));
        Assert.Equal("list_string", AttributeWriter.ValueTypeName(PropertyValueType.ListString));
        Assert.Equal("bool", AttributeWriter.ValueTypeName(PropertyValueType.Bool));
    }

    [Fact]
    public void Describe_Property_EchoesValueTypeAndUsage()
    {
        _props.RegisterEngineProperty("cookable", "Cookable", PropertyValueType.Bool, appliesTo: new[] { "item" });
        var target = new Entity("item", "Steak");
        target.SetProperty("cookable", true);

        var result = Writer.Describe(target, "cookable");

        Assert.True(result.Ok);
        Assert.Contains("cookable on Steak = true", result.Message);
        Assert.Contains("(bool, applies to item)", result.Message);
        Assert.Contains("Usage: set item cookable <target> <true|false>", result.Message);
    }

    [Fact]
    public void Describe_UnsetProperty_ShowsUnset()
    {
        _props.RegisterEngineProperty("cookable", "Cookable", PropertyValueType.Bool, appliesTo: new[] { "item" });
        var target = new Entity("item", "Steak");

        var result = Writer.Describe(target, "cookable");

        Assert.True(result.Ok);
        Assert.Contains("= (unset)", result.Message);
    }

    [Fact]
    public void Describe_UnknownAttribute_Rejected()
    {
        var target = new Entity("item", "Steak");
        var result = Writer.Describe(target, "nope");
        Assert.False(result.Ok);
        Assert.Contains("Unknown attribute", result.Message);
    }

    [Fact]
    public void Write_RejectsTransientProperty_AsEngineManaged()
    {
        // Transient = engine-managed runtime bookkeeping; declared for type metadata but not hand-settable.
        _props.RegisterEngineProperty("following", "Entity being followed", PropertyValueType.String,
            appliesTo: new[] { "player" }, transient: true);
        var target = new Entity("player", "Rocky");

        var result = Writer.Write(target, "following", new[] { "someone" });

        Assert.False(result.Ok);
        Assert.Contains("engine-managed", result.Message);
        Assert.Null(target.GetProperty<string>("following"));
    }

    [Fact]
    public void Write_RejectsNonSettableProperty_AsEngineManaged()
    {
        // settable: false marks a persisted-but-not-hand-settable field (e.g. template_id).
        _props.RegisterEngineProperty("template_id", "Template ID", PropertyValueType.String, settable: false);
        var target = new Entity("item", "Sword");

        var result = Writer.Write(target, "template_id", new[] { "core:other" });

        Assert.False(result.Ok);
        Assert.Contains("engine-managed", result.Message);
        Assert.Null(target.GetProperty<string>("template_id"));
    }

    [Fact]
    public void Write_AmbiguousBareName_AcrossTwoPacks_IsRejectedNotFirstWins()
    {
        // Two packs declare the SAME bare property name. Namespace-aware storage keeps them
        // DISTINCT ("pack-a:loyalty", "pack-b:loyalty") -- not a registration collision.
        // But an admin `set npc loyalty <target> 5` has NO pack context, so the bare name is
        // unresolvable-by-design and MUST be rejected with a located diagnostic -- NOT silently
        // first-won (the GetAll().FirstOrDefault() defect).
        _props.RegisterPackProperty("pack-a", "loyalty", "A-loyalty", PropertyValueType.Int,
            appliesTo: new[] { "npc" });
        _props.RegisterPackProperty("pack-b", "loyalty", "B-loyalty", PropertyValueType.Int,
            appliesTo: new[] { "npc" });
        var target = new Entity("npc", "Guard");

        var result = Writer.Write(target, "loyalty", new[] { "5" });

        Assert.False(result.Ok);
        Assert.Contains("ambiguous", result.Message);
        Assert.Contains("pack-a", result.Message);
        Assert.Contains("pack-b", result.Message);
        // The defect would have silently written the value via the first-wins entry.
        Assert.Equal(0, target.GetProperty<int>("loyalty"));
    }
}
