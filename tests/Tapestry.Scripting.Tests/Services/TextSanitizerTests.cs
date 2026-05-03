using Tapestry.Scripting.Services;
using Xunit;

namespace Tapestry.Scripting.Tests.Services;

public class TextSanitizerTests
{
    [Fact]
    public void Strip_removes_ansi_sgr_codes()
    {
        var input = "\x1B[1;32mGreen bold text\x1B[0m";
        Assert.Equal("Green bold text", TextSanitizer.Strip(input));
    }

    [Fact]
    public void Strip_removes_ansi_clear_and_cursor_codes()
    {
        var input = "\x1B[2J\x1B[H Some text";
        Assert.Equal(" Some text", TextSanitizer.Strip(input));
    }

    [Fact]
    public void Strip_removes_custom_markup_tags()
    {
        var input = "<highlight>Room Name</highlight>";
        Assert.Equal("Room Name", TextSanitizer.Strip(input));
    }

    [Fact]
    public void Strip_removes_npc_and_item_tags()
    {
        var input = "<npc>The Blacksmith is here.</npc>";
        Assert.Equal("The Blacksmith is here.", TextSanitizer.Strip(input));
    }

    [Fact]
    public void Strip_removes_direction_tag()
    {
        var input = "<direction>[Exits: north south]</direction>";
        Assert.Equal("[Exits: north south]", TextSanitizer.Strip(input));
    }

    [Fact]
    public void Strip_leaves_plain_text_unchanged()
    {
        var input = "You buy a sword for 100 gold.";
        Assert.Equal(input, TextSanitizer.Strip(input));
    }

    [Fact]
    public void Strip_handles_combined_ansi_and_markup()
    {
        var input = "\x1B[32m<npc>The Blacksmith is here.</npc>\x1B[0m";
        Assert.Equal("The Blacksmith is here.", TextSanitizer.Strip(input));
    }

    [Fact]
    public void Strip_handles_empty_string()
    {
        Assert.Equal("", TextSanitizer.Strip(""));
    }
}
