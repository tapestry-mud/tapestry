using FluentAssertions;
using Tapestry.Engine.Inventory;

namespace Tapestry.Engine.Tests.Inventory;

public class RarityRegistryTests
{
    [Fact]
    public void GetTier_ReturnsRegisteredTier()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("common", 0, null, null, "white", false));
        registry.GetTier("common").Should().NotBeNull();
        registry.GetTier("common")!.Key.Should().Be("common");
    }

    [Fact]
    public void GetTier_UnknownKeyReturnsNull()
    {
        var registry = new RarityRegistry();
        registry.GetTier("legendary").Should().BeNull();
    }

    [Fact]
    public void TagWidth_IsZeroWhenNoVisibleTiersRegistered()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("common", 0, null, null, "white", false));
        registry.TagWidth.Should().Be(0);
    }

    [Fact]
    public void TagWidth_IsMaxRenderedWidthAcrossVisibleTiers()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("uncommon", 1, "Uncommon", ("-= ", " =-"), "white", true));
        registry.Register(new RarityTierDefinition("rare", 2, "Rare", ("-= ", " =-"), "green", true));
        // Uncommon: 3 + 8 + 3 = 14. Rare padded to 8 = 3 + 8 + 3 = 14.
        registry.TagWidth.Should().Be(14);
    }

    [Fact]
    public void Format_VisibleTier_ReturnsPaddedColorTag()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("uncommon", 1, "Uncommon", ("-= ", " =-"), "white", true));
        registry.Register(new RarityTierDefinition("rare", 2, "Rare", ("-= ", " =-"), "green", true));
        var result = registry.Format("rare");
        result.Should().Contain("Rare");
        result.Should().Contain("<");
    }

    [Fact]
    public void Format_InvisibleTier_ReturnsWhitespacePadding()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("common", 0, null, null, "white", false));
        registry.Register(new RarityTierDefinition("uncommon", 1, "Uncommon", ("-= ", " =-"), "white", true));
        var result = registry.Format("common");
        result.Should().Be(new string(' ', registry.TagWidth));
    }

    [Fact]
    public void Format_UnknownKey_ReturnsWhitespacePadding()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("uncommon", 1, "Uncommon", ("-= ", " =-"), "white", true));
        var result = registry.Format("garbage");
        result.Should().Be(new string(' ', registry.TagWidth));
    }

    [Fact]
    public void Format_NoTiersRegistered_ReturnsEmptyString()
    {
        var registry = new RarityRegistry();
        registry.Format("anything").Should().Be(string.Empty);
    }

    [Fact]
    public void FormatInline_VisibleTier_ReturnsNaturalWidthTag()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("rare", 2, "Rare", ("-= ", " =-"), "green", true));
        var result = registry.FormatInline("rare");
        result.Should().Contain("Rare");
        result.Should().Contain("<");
    }

    [Fact]
    public void FormatInline_InvisibleTier_ReturnsEmptyString()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("common", 0, null, null, "white", false));
        registry.FormatInline("common").Should().Be(string.Empty);
    }

    [Fact]
    public void FormatInline_NullKey_ReturnsEmptyString()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("rare", 2, "Rare", ("-= ", " =-"), "green", true));
        registry.FormatInline(null).Should().Be(string.Empty);
    }

    [Fact]
    public void FormatInline_UnknownKey_ReturnsEmptyString()
    {
        var registry = new RarityRegistry();
        registry.FormatInline("garbage").Should().Be(string.Empty);
    }

    [Fact]
    public void FormatInline_EmitsSemanticTag()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("rare", 2, "Rare", ("-= ", " =-"), "green", true));

        var result = registry.FormatInline("rare");

        result.Should().StartWith("<item.rare>");
        result.Should().EndWith("</item.rare>");
        result.Should().Contain("-= Rare =-");
    }

    [Fact]
    public void FormatInline_DoesNotEmitColorTag()
    {
        var registry = new RarityRegistry();
        registry.Register(new RarityTierDefinition("rare", 2, "Rare", ("-= ", " =-"), "green", true));

        var result = registry.FormatInline("rare");

        result.Should().NotContain("<color");
    }
}
