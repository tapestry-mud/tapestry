using FluentAssertions;
using Tapestry.Engine.Color;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Color;

public class ColorRenderingConnectionTests
{
    private static ColorRenderer MakeRenderer()
    {
        var theme = new ThemeRegistry();
        theme.Register("highlight", new ThemeEntry { Fg = "bright-white" });
        theme.Compile();
        return new ColorRenderer(theme);
    }

    [Fact]
    public void SendLine_AnsiCapable_RendersAnsi()
    {
        var renderer = MakeRenderer();
        var inner = new FakeConnection { SupportsAnsi = true };
        var connection = new ColorRenderingConnection(inner, renderer);

        connection.SendLine("<highlight>Town Square</highlight>");

        inner.SentText.Should().ContainSingle()
            .Which.Should().Be("\x1b[97mTown Square\x1b[0m\r\n");
    }

    [Fact]
    public void SendLine_PlainClient_StripsTagsToPlainText()
    {
        var renderer = MakeRenderer();
        var inner = new FakeConnection { SupportsAnsi = false };
        var connection = new ColorRenderingConnection(inner, renderer);

        connection.SendLine("<highlight>Town Square</highlight>");

        inner.SentText.Should().ContainSingle()
            .Which.Should().Be("Town Square\r\n");
    }

    [Fact]
    public void SendText_AnsiCapable_RendersAnsi()
    {
        var renderer = MakeRenderer();
        var inner = new FakeConnection { SupportsAnsi = true };
        var connection = new ColorRenderingConnection(inner, renderer);

        connection.SendText("<highlight>Hello</highlight>");

        inner.SentText.Should().ContainSingle()
            .Which.Should().Be("\x1b[97mHello\x1b[0m");
    }

    [Fact]
    public void SendText_PlainClient_StripsTagsToPlainText()
    {
        var renderer = MakeRenderer();
        var inner = new FakeConnection { SupportsAnsi = false };
        var connection = new ColorRenderingConnection(inner, renderer);

        connection.SendText("<highlight>Hello</highlight>");

        inner.SentText.Should().ContainSingle()
            .Which.Should().Be("Hello");
    }

    [Fact]
    public void Id_DelegatesToInner()
    {
        var inner = new FakeConnection();
        var connection = new ColorRenderingConnection(inner, MakeRenderer());
        connection.Id.Should().Be(inner.Id);
    }

    [Fact]
    public void IsConnected_DelegatesToInner()
    {
        var inner = new FakeConnection();
        var connection = new ColorRenderingConnection(inner, MakeRenderer());
        connection.IsConnected.Should().BeTrue();
        inner.Disconnect("test");
        connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void SupportsAnsi_DelegatesToInner()
    {
        var inner = new FakeConnection { SupportsAnsi = true };
        var connection = new ColorRenderingConnection(inner, MakeRenderer());
        connection.SupportsAnsi.Should().BeTrue();
    }

    [Fact]
    public void Disconnect_DelegatesToInner()
    {
        var inner = new FakeConnection();
        var connection = new ColorRenderingConnection(inner, MakeRenderer());
        connection.Disconnect("reason");
        inner.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void OnInput_FiresWhenInnerFires()
    {
        var inner = new FakeConnection();
        var connection = new ColorRenderingConnection(inner, MakeRenderer());
        var received = new List<string>();
        connection.OnInput += s => received.Add(s);

        inner.SimulateInput("hello");

        received.Should().ContainSingle("hello");
    }

    [Fact]
    public void OnDisconnected_FiresWhenInnerDisconnects()
    {
        var inner = new FakeConnection();
        var connection = new ColorRenderingConnection(inner, MakeRenderer());
        var fired = false;
        connection.OnDisconnected += () => fired = true;

        inner.Disconnect("test");

        fired.Should().BeTrue();
    }

    [Fact]
    public void SuppressEcho_DelegatesToInner_NoThrow()
    {
        var inner = new FakeConnection();
        var connection = new ColorRenderingConnection(inner, MakeRenderer());
        var act = () => connection.SuppressEcho();
        act.Should().NotThrow();
    }

    [Fact]
    public void PlainText_NoTags_PassesThroughUnchanged()
    {
        var inner = new FakeConnection { SupportsAnsi = true };
        var connection = new ColorRenderingConnection(inner, MakeRenderer());
        connection.SendLine("What is your name, adventurer?");
        inner.SentText.Should().ContainSingle()
            .Which.Should().Be("What is your name, adventurer?\r\n");
    }
}
