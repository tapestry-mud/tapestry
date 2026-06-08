using FluentAssertions;
using Tapestry.Engine.Color;
using Tapestry.Engine.Text;

namespace Tapestry.Engine.Tests.Text;

public class WrappingConnectionTests
{
    private static MarkupWrapper Wrapper()
    {
        var theme = new ThemeRegistry();
        theme.Register("highlight", new ThemeEntry { Fg = "bright-white" });
        theme.Compile();
        return new MarkupWrapper(theme);
    }

    [Fact]
    public void SendText_WrapsToWidth()
    {
        var inner = new FakeConnection();
        var conn = new WrappingConnection(inner, Wrapper(), () => 9);
        conn.SendText("the quick brown fox");
        inner.SentLines.Should().ContainSingle().Which.Should().Be("the quick\r\nbrown fox");
    }

    [Fact]
    public void SendLine_WrapsToWidth_InnerAddsTerminator()
    {
        var inner = new FakeConnection();
        var conn = new WrappingConnection(inner, Wrapper(), () => 9);
        conn.SendLine("the quick brown fox");
        inner.SentLines.Should().ContainSingle().Which.Should().Be("the quick\r\nbrown fox\r\n");
    }

    [Fact]
    public void WidthZero_Passthrough()
    {
        var inner = new FakeConnection();
        var conn = new WrappingConnection(inner, Wrapper(), () => 0);
        conn.SendText("the quick brown fox jumps");
        inner.SentLines.Should().ContainSingle().Which.Should().Be("the quick brown fox jumps");
    }

    [Fact]
    public void WidthResolvedPerCall()
    {
        var inner = new FakeConnection();
        var width = 80;
        var conn = new WrappingConnection(inner, Wrapper(), () => width);
        conn.SendText("the quick brown fox");
        width = 9;
        conn.SendText("the quick brown fox");
        inner.SentLines[0].Should().Be("the quick brown fox");   // width 80: untouched
        inner.SentLines[1].Should().Be("the quick\r\nbrown fox"); // width 9: wrapped
    }

    [Fact]
    public void DelegatesIdentityAndState()
    {
        var inner = new FakeConnection("conn-123") { SupportsAnsi = true, RemoteAddress = "1.2.3.4" };
        var conn = new WrappingConnection(inner, Wrapper(), () => 80);

        conn.Id.Should().Be("conn-123");
        conn.SupportsAnsi.Should().BeTrue();
        conn.RemoteAddress.Should().Be("1.2.3.4");
        conn.IsConnected.Should().BeTrue();

        conn.Disconnect("bye");
        conn.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void ForwardsInputEvents()
    {
        var inner = new FakeConnection();
        var conn = new WrappingConnection(inner, Wrapper(), () => 80);
        string? got = null;
        conn.OnInput += s => got = s;
        inner.SimulateInput("look");
        got.Should().Be("look");
    }
}
