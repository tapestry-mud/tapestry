using FluentAssertions;
using Tapestry.Engine.Inventory;

namespace Tapestry.Engine.Tests.Inventory;

public class EssenceRegistryTests
{
    [Fact]
    public void GetEssence_ReturnsRegisteredEssence()
    {
        var registry = new EssenceRegistry();
        registry.Register(new EssenceDefinition("fire", "^", "red"));
        registry.GetEssence("fire").Should().NotBeNull();
        registry.GetEssence("fire")!.Glyph.Should().Be("^");
    }

    [Fact]
    public void GetEssence_UnknownKeyReturnsNull()
    {
        var registry = new EssenceRegistry();
        registry.GetEssence("void").Should().BeNull();
    }

    [Fact]
    public void Format_KnownKey_ReturnsColoredGlyph()
    {
        var registry = new EssenceRegistry();
        registry.Register(new EssenceDefinition("fire", "^", "red"));
        var result = registry.Format("fire");
        result.Should().Contain("^");
        result.Should().Contain("<");
    }

    [Fact]
    public void Format_RendersGlyphInParens()
    {
        var registry = new EssenceRegistry();
        registry.Register(new EssenceDefinition("shadow", "~", "magenta"));
        var result = registry.Format("shadow");
        result.Should().Contain("(~)");
    }

    [Fact]
    public void Format_EmitsSemanticTag()
    {
        var registry = new EssenceRegistry();
        registry.Register(new EssenceDefinition("fire", "^", "red"));

        var result = registry.Format("fire");

        result.Should().StartWith("<essence.fire>");
        result.Should().EndWith("</essence.fire>");
        result.Should().Contain("(^)");
    }

    [Fact]
    public void Format_DoesNotEmitColorTag()
    {
        var registry = new EssenceRegistry();
        registry.Register(new EssenceDefinition("fire", "^", "red"));

        var result = registry.Format("fire");

        result.Should().NotContain("<color");
    }

    [Fact]
    public void Format_NullKey_ReturnsEmpty()
    {
        var registry = new EssenceRegistry();
        registry.Format(null).Should().BeEmpty();
    }

    [Fact]
    public void Format_UnknownKey_ReturnsEmpty()
    {
        var registry = new EssenceRegistry();
        registry.Format("unknown").Should().BeEmpty();
    }
}
