using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;
using Tapestry.Shared;
using System.Text.Json;

namespace Tapestry.Engine.Tests.Gmcp;

public class GmcpConnectionManagerTests
{
    private class FakeIGmcpHandler : IGmcpHandler
    {
        public bool GmcpActive { get; set; } = true;
        public List<(string Package, object Payload)> Sent { get; } = new();
        public Action<string, JsonElement>? OnGmcpMessage { get; set; }
        public void Send(string package, object payload) { Sent.Add((package, payload)); }
        public bool SupportsPackage(string package) => true;
    }

    [Fact]
    public void Send_ByConnectionId_DeliversToRegisteredHandler()
    {
        var sessions = new SessionManager();
        var cm = new GmcpConnectionManager(sessions);
        var fake = new FakeIGmcpHandler();
        cm.RegisterHandler("conn1", fake);

        cm.Send("conn1", "Test.Package", new { value = 1 });

        fake.Sent.Should().ContainSingle(x => x.Package == "Test.Package");
    }

    [Fact]
    public void Send_ByEntityId_ResolvesConnectionViaSessionManager()
    {
        var sessions = new SessionManager();
        var cm = new GmcpConnectionManager(sessions);
        var fake = new FakeIGmcpHandler();

        var entity = new Entity("player", "Test");
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity);
        sessions.Add(session);
        cm.RegisterHandler(conn.Id, fake);

        cm.Send(entity.Id, "Test.Package", new { });

        fake.Sent.Should().ContainSingle(x => x.Package == "Test.Package");
    }

    [Fact]
    public void UnregisterHandler_StopsDelivery()
    {
        var sessions = new SessionManager();
        var cm = new GmcpConnectionManager(sessions);
        var fake = new FakeIGmcpHandler();
        cm.RegisterHandler("conn1", fake);
        cm.UnregisterHandler("conn1");

        cm.Send("conn1", "Test.Package", new { });

        fake.Sent.Should().BeEmpty();
    }

    [Fact]
    public void SupportsPackage_ReturnsFalse_WhenEntityHasNoSession()
    {
        var sessions = new SessionManager();
        var cm = new GmcpConnectionManager(sessions);

        var result = cm.SupportsPackage(Guid.NewGuid(), "Any.Package");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetActiveConnectionIds_ReturnsRegisteredIds()
    {
        var sessions = new SessionManager();
        var cm = new GmcpConnectionManager(sessions);
        var fake = new FakeIGmcpHandler();
        cm.RegisterHandler("conn-a", fake);
        cm.RegisterHandler("conn-b", fake);

        cm.GetActiveConnectionIds().Should().Contain("conn-a").And.Contain("conn-b");
    }
}
