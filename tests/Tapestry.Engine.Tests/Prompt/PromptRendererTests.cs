// tests/Tapestry.Engine.Tests/Prompt/PromptRendererTests.cs
using Tapestry.Engine.Prompt;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Prompt;

public class PromptRendererTests
{
    [Fact]
    public void Render_ReplacesAllTokens()
    {
        var renderer = new PromptRenderer();
        var entity = new Entity("player", "TestPlayer");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 85;
        entity.Stats.BaseMaxResource = 50;
        entity.Stats.Resource = 50;
        entity.Stats.BaseMaxMovement = 80;
        entity.Stats.Movement = 70;

        var template = "<hp>{hp}/{maxhp}</hp> | <mana>{mana}/{maxmana}</mana> | <mv>{mv}/{maxmv}</mv>>";
        var result = renderer.Render(template, entity);

        Assert.Equal("<hp>85/100</hp> | <mana>50/50</mana> | <mv>70/80</mv>>", result);
    }

    [Fact]
    public void Render_InvalidToken_RendersEmpty()
    {
        var renderer = new PromptRenderer();
        var entity = new Entity("player", "TestPlayer");

        var result = renderer.Render("{bogus}> ", entity);

        Assert.Equal("> ", result);
    }

    [Fact]
    public void Render_NoTokens_ReturnsTemplateAsIs()
    {
        var renderer = new PromptRenderer();
        var entity = new Entity("player", "TestPlayer");

        var result = renderer.Render("> ", entity);

        Assert.Equal("> ", result);
    }

    [Fact]
    public void Render_UsesEffectiveMaxValues_WithModifiers()
    {
        var renderer = new PromptRenderer();
        var entity = new Entity("player", "TestPlayer");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.Hp = 85;
        entity.Stats.AddModifier(new StatModifier("test", StatType.MaxHp, 20));

        var result = renderer.Render("{hp}/{maxhp}>", entity);

        Assert.Equal("85/120>", result);
    }

    [Fact]
    public void GetDefaultTemplate_ReturnsExpectedFormat()
    {
        Assert.Equal(
            "<hp>[HP]: {hp}/{maxhp}</hp> | <mana>[Mana]: {mana}/{maxmana}</mana> | <mv>[Mv]: {mv}/{maxmv}</mv>> ",
            PromptRenderer.DefaultTemplate);
    }
}
