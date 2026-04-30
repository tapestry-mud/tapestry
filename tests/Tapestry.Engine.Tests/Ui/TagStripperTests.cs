using FluentAssertions;
using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Tests.Ui;

public class TagStripperTests
{
    [Fact]
    public void StripTags_NoTags_ReturnsOriginal()
    {
        TagStripper.StripTags("hello world").Should().Be("hello world");
    }

    [Fact]
    public void StripTags_SingleTag_RemovesBothBrackets()
    {
        TagStripper.StripTags("<highlight>HP</highlight>").Should().Be("HP");
    }

    [Fact]
    public void StripTags_MixedContent_PreservesPlainText()
    {
        TagStripper.StripTags("<a>b</a>c").Should().Be("bc");
    }

    [Fact]
    public void StripTags_EmptyString_ReturnsEmpty()
    {
        TagStripper.StripTags("").Should().Be("");
    }

    [Fact]
    public void StripTags_MultipleTagPairs_StripsAll()
    {
        TagStripper.StripTags("<hp>340</hp> / <mana>120</mana>").Should().Be("340 / 120");
    }

    [Fact]
    public void VisibleLength_NoTags_ReturnsStringLength()
    {
        TagStripper.VisibleLength("hello").Should().Be(5);
    }

    [Fact]
    public void VisibleLength_WithTags_CountsOnlyVisibleChars()
    {
        TagStripper.VisibleLength("<highlight>HP: 340/340</highlight>").Should().Be(11);
    }

    [Fact]
    public void VisibleLength_EmptyString_ReturnsZero()
    {
        TagStripper.VisibleLength("").Should().Be(0);
    }

    [Fact]
    public void StripTags_UnclosedTagAtEnd_SkipsToEnd()
    {
        // Malformed input — tag with no closing > — treat < and rest as literal
        TagStripper.StripTags("text<open").Should().Be("text<open");
    }
}
