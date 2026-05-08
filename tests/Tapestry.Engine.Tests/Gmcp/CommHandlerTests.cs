using FluentAssertions;
using System.Text.Json;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class CommHandlerTests
{
    [Fact]
    public void SendChannel_SendsCommChannelPackage()
    {
        var cm = new FakeGmcpConnectionManager();
        var handler = new CommHandler(cm);

        var entityId = Guid.NewGuid();
        handler.SendChannel(entityId, "gossip", "Rye", "Hello world");

        cm.Sent.Should().ContainSingle(x => x.Package == "Comm.Channel");
    }

    [Fact]
    public void SendChannel_IncludesChannelSenderAndText()
    {
        var cm = new FakeGmcpConnectionManager();
        var handler = new CommHandler(cm);

        var entityId = Guid.NewGuid();
        handler.SendChannel(entityId, "gossip", "Rye", "Hello world");

        var sent = cm.Sent.First(x => x.Package == "Comm.Channel");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("channel").GetString().Should().Be("gossip");
        doc.RootElement.GetProperty("sender").GetString().Should().Be("Rye");
        doc.RootElement.GetProperty("text").GetString().Should().Be("Hello world");
    }

    [Fact]
    public void SendBurst_IsNoOp()
    {
        var cm = new FakeGmcpConnectionManager();
        var handler = new CommHandler(cm);

        handler.SendBurst("conn1", new Entity("player", "Test"));

        cm.Sent.Should().BeEmpty();
    }

    [Fact]
    public void PackageNames_ContainsCommChannel()
    {
        var handler = new CommHandler(new FakeGmcpConnectionManager());
        handler.PackageNames.Should().Contain("Comm.Channel");
    }
}
