using FluentAssertions;
using Tapestry.Engine.Authoring;

namespace Tapestry.Engine.Tests;

public class RoomSluggerTests
{
    // The exact table from the design spec, plus rule-by-rule cases.
    [Theory]
    [InlineData("The Gatehouse", "gatehouse")]                  // article drop
    [InlineData("The Raven's Roost", "ravens-roost")]           // apostrophe strip + article
    [InlineData("Best room Ever", "best-room-ever")]            // lowercase + hyphens
    [InlineData("A Quiet Corner", "quiet-corner")]              // 'a' article
    [InlineData("An Empty Hall", "empty-hall")]                 // 'an' article
    [InlineData("Guard Post", "guard-post")]                    // plain two-word
    [InlineData("  Padded   Name  ", "padded-name")]            // whitespace runs collapse
    [InlineData("Room #3 (East Wing)", "room-3-east-wing")]     // punctuation runs -> single hyphen
    [InlineData("UPPERCASE", "uppercase")]                      // lowercase rule
    [InlineData("The", "the")]                                  // bare article: nothing follows, keep it
    public void Slugify_AppliesAllRules(string name, string expected)
    {
        RoomSlugger.Slugify(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    public void Slugify_ReturnsNull_WhenNothingUsableSurvives(string name)
    {
        RoomSlugger.Slugify(name).Should().BeNull();
    }

    [Fact]
    public void Disambiguate_ReturnsSlugUnchanged_WhenFree()
    {
        RoomSlugger.Disambiguate("gatehouse", _ => false).Should().Be("gatehouse");
    }

    [Fact]
    public void Disambiguate_AppendsTwo_WhenTaken()
    {
        var taken = new HashSet<string> { "gatehouse" };
        RoomSlugger.Disambiguate("gatehouse", taken.Contains).Should().Be("gatehouse-2");
    }

    [Fact]
    public void Disambiguate_CountsUp_UntilFree()
    {
        var taken = new HashSet<string> { "gatehouse", "gatehouse-2", "gatehouse-3" };
        RoomSlugger.Disambiguate("gatehouse", taken.Contains).Should().Be("gatehouse-4");
    }
}
