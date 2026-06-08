using System.Text.RegularExpressions;
using FluentAssertions;
using Tapestry.Engine.Color;
using Tapestry.Engine.Text;

namespace Tapestry.Engine.Tests.Text;

// End-to-end through the real decorator stack: WrappingConnection -> ColorRenderingConnection
// -> sink. Verifies wrapping (on markup) and color rendering compose correctly, including a
// colored span that wraps across an inserted line break.
public class WrapColorIntegrationTests
{
    private static (WrappingConnection conn, FakeConnection sink) Build(int width, bool ansi)
    {
        var theme = new ThemeRegistry();
        theme.Register("highlight", new ThemeEntry { Fg = "bright-white" });
        theme.Register("direction", new ThemeEntry { Fg = "cyan" });
        theme.Compile();
        var renderer = new ColorRenderer(theme);
        var wrapper = new MarkupWrapper(theme);
        var sink = new FakeConnection { SupportsAnsi = ansi };
        var conn = new WrappingConnection(new ColorRenderingConnection(sink, renderer), wrapper, () => width);
        return (conn, sink);
    }

    [Fact]
    public void RoomDescription_WrapsToWidth_AndRendersColor()
    {
        var (conn, sink) = Build(40, ansi: true);
        var room =
            "<highlight>The Town Square</highlight>\r\n" +
            "A wide cobbled plaza opens before you, ringed by timbered shops whose painted signs " +
            "creak gently in the breeze. A weathered fountain mutters at its center.\r\n" +
            "<direction>[Exits: north east south]</direction>";

        conn.SendText(room);

        var output = sink.SentLines.Should().ContainSingle().Subject;
        output.Should().Contain("\x1b[");   // color rendered for an ANSI client

        var stripped = Regex.Replace(output, "\x1b\\[[0-9;]*m", "");
        foreach (var line in stripped.Split("\r\n"))
        {
            line.Length.Should().BeLessThanOrEqualTo(40);
        }

        // Words preserved intact: no hyphenation, key words survive whole.
        stripped.Replace("\r\n", " ").Should().Contain("cobbled").And.Contain("fountain").And.NotContain("-");
    }

    [Fact]
    public void ColoredSpan_WrappedAcrossLines_RendersContiguousColor()
    {
        var (conn, sink) = Build(9, ansi: true);
        conn.SendText("<highlight>the quick brown fox</highlight>");

        // Open code once at the start, reset once at the end, the wrap newline inside the span.
        sink.SentLines.Should().ContainSingle().Which
            .Should().Be("\x1b[97mthe quick\r\nbrown fox\x1b[0m");
    }

    [Fact]
    public void PlainClient_StripsColor_AndWraps()
    {
        var (conn, sink) = Build(20, ansi: false);
        conn.SendText("<highlight>the quick brown fox jumps over</highlight>");

        var output = sink.SentLines.Should().ContainSingle().Subject;
        output.Should().NotContain("\x1b");        // color stripped for non-ANSI client
        output.Should().NotContain("<highlight>"); // tags stripped
        foreach (var line in output.Split("\r\n"))
        {
            line.Length.Should().BeLessThanOrEqualTo(20);
        }
    }
}
