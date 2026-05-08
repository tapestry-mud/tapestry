using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Color;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class DisplayHandlerTests
{
    [Fact]
    public void SendBurst_SendsWorldDisplayColors()
    {
        var cm = new FakeGmcpConnectionManager();
        var theme = new ThemeRegistry();
        var handler = new DisplayHandler(cm, theme);

        var entity = new Entity("player", "Test");
        handler.SendBurst("conn1", entity);

        cm.Sent.Should().ContainSingle(x => x.Package == "World.Display.Colors");
    }

    [Fact]
    public void PackageNames_ContainsWorldDisplayColors()
    {
        var handler = new DisplayHandler(new FakeGmcpConnectionManager(), new ThemeRegistry());
        handler.PackageNames.Should().Contain("World.Display.Colors");
    }
}
