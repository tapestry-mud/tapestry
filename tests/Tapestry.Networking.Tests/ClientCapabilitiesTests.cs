using FluentAssertions;
using Tapestry.Networking;

namespace Tapestry.Networking.Tests;

public class ClientCapabilitiesTests
{
    [Theory]
    [InlineData("zmud", true)]
    [InlineData("ZMUD", true)]
    [InlineData("cmud", true)]
    [InlineData("Mudlet", true)]
    [InlineData("MUSHCLIENT", true)]
    [InlineData("tintin++", true)]
    [InlineData("tintin", true)]
    [InlineData("ANSI", false)]
    [InlineData("VT100", false)]
    [InlineData("xterm-256color", false)]
    [InlineData("unknown-terminal", false)]
    public void IsMudClient_detects_known_clients(string ttype, bool expected)
    {
        var caps = ClientCapabilities.FromNegotiation(ttype, 80, 24);
        caps.IsMudClient.Should().Be(expected);
    }

    [Theory]
    [InlineData("zmud", ColorSupport.Extended)]
    [InlineData("cmud", ColorSupport.Extended)]
    [InlineData("mudlet", ColorSupport.Extended)]
    [InlineData("ANSI", ColorSupport.Basic)]
    [InlineData("VT100", ColorSupport.Basic)]
    [InlineData("xterm", ColorSupport.Basic)]
    [InlineData("xterm-256color", ColorSupport.Extended)]
    [InlineData("XTERM-256COLOR", ColorSupport.Extended)]
    [InlineData("xterm-truecolor", ColorSupport.TrueColor)]
    [InlineData("unknown-thing", ColorSupport.Basic)]
    public void ColorSupport_derived_from_ttype(string ttype, ColorSupport expected)
    {
        var caps = ClientCapabilities.FromNegotiation(ttype, 80, 24);
        caps.ColorSupport.Should().Be(expected);
    }

    [Fact]
    public void UseServerEcho_true_for_terminals()
    {
        var caps = ClientCapabilities.FromNegotiation("ANSI", 116, 60);
        caps.UseServerEcho.Should().BeTrue();
    }

    [Fact]
    public void UseServerEcho_false_for_mud_clients()
    {
        var caps = ClientCapabilities.FromNegotiation("zmud", 312, 94);
        caps.UseServerEcho.Should().BeFalse();
    }

    [Fact]
    public void FromNegotiation_stores_window_dimensions()
    {
        var caps = ClientCapabilities.FromNegotiation("ANSI", 120, 40);
        caps.WindowWidth.Should().Be(120);
        caps.WindowHeight.Should().Be(40);
    }

    [Fact]
    public void FromNegotiation_stores_ttype_name()
    {
        var caps = ClientCapabilities.FromNegotiation("ANSI", 80, 24);
        caps.ClientName.Should().Be("ANSI");
        caps.SupportsTtype.Should().BeTrue();
        caps.SupportsNaws.Should().BeTrue();
    }

    [Fact]
    public void Default_returns_no_capabilities()
    {
        var caps = ClientCapabilities.Default;
        caps.ClientName.Should().BeNull();
        caps.SupportsTtype.Should().BeFalse();
        caps.SupportsNaws.Should().BeFalse();
        caps.SupportsGmcp.Should().BeFalse();
        caps.WindowWidth.Should().Be(80);
        caps.WindowHeight.Should().Be(24);
        caps.ColorSupport.Should().Be(ColorSupport.None);
        caps.UseServerEcho.Should().BeTrue();
        caps.IsMudClient.Should().BeFalse();
    }

    [Fact]
    public void FromTimeout_returns_server_echo_no_color()
    {
        var caps = ClientCapabilities.FromTimeout();
        caps.ClientName.Should().BeNull();
        caps.SupportsTtype.Should().BeFalse();
        caps.SupportsNaws.Should().BeFalse();
        caps.SupportsGmcp.Should().BeFalse();
        caps.WindowWidth.Should().Be(80);
        caps.WindowHeight.Should().Be(24);
        caps.ColorSupport.Should().Be(ColorSupport.None);
        caps.UseServerEcho.Should().BeTrue();
        caps.IsMudClient.Should().BeFalse();
    }

    [Fact]
    public void FromNegotiation_with_naws_only()
    {
        var caps = ClientCapabilities.FromNegotiation(null, 120, 40);
        caps.SupportsTtype.Should().BeFalse();
        caps.SupportsNaws.Should().BeTrue();
        caps.ColorSupport.Should().Be(ColorSupport.None);
        caps.UseServerEcho.Should().BeTrue();
    }

    [Fact]
    public void FromNegotiation_with_ttype_only()
    {
        var caps = ClientCapabilities.FromNegotiation("ANSI", null, null);
        caps.SupportsTtype.Should().BeTrue();
        caps.SupportsNaws.Should().BeFalse();
        caps.WindowWidth.Should().Be(80);
        caps.WindowHeight.Should().Be(24);
    }

    [Fact]
    public void FromNegotiation_sets_gmcp_supported_when_flag_true()
    {
        var caps = ClientCapabilities.FromNegotiation("mudlet", 80, 24, gmcpSupported: true);

        caps.SupportsGmcp.Should().BeTrue();
    }

    [Fact]
    public void FromNegotiation_does_not_set_gmcp_by_default()
    {
        var caps = ClientCapabilities.FromNegotiation("mudlet", 80, 24);

        caps.SupportsGmcp.Should().BeFalse();
    }
}
