using FluentAssertions;
using System.Text.Json;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Sustenance;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class CharStatusHandlerTests
{
    private record Harness(
        CharStatusHandler Handler,
        FakeGmcpConnectionManager ConnectionManager,
        DirtyVitalsBatcher Batcher,
        SessionManager Sessions,
        EventBus EventBus,
        Entity Player,
        string ConnectionId);

    private static Harness Build()
    {
        var cm = new FakeGmcpConnectionManager();
        var batcher = new DirtyVitalsBatcher();
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        var progression = new ProgressionManager(world, eb);
        var alignment = new AlignmentManager(world, eb, new AlignmentConfig());
        var sustenance = new SustenanceConfig();

        var handler = new CharStatusHandler(cm, batcher, sessions, world, eb, progression, alignment, sustenance);

        var entity = new Entity("player", "Hero");
        world.TrackEntity(entity);
        var conn = new FakeConnection();
        sessions.Add(new PlayerSession(conn, entity));
        handler.Configure();

        return new Harness(handler, cm, batcher, sessions, eb, entity, conn.Id);
    }

    [Fact]
    public void SendBurst_SendsCharStatusVarsAndCharStatus()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.StatusVars");
        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Status");
    }

    [Fact]
    public void SendBurst_CharStatus_ContainsEntityName()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        var sent = h.ConnectionManager.Sent.First(x => x.Package == "Char.Status");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Hero");
    }

    [Fact]
    public void LevelUpEvent_SendsCharStatusAndMarksDirty()
    {
        var h = Build();
        var dirtied = new List<Guid>();
        h.Batcher.SetFlushCallback(id => dirtied.Add(id));

        h.EventBus.Publish(new GameEvent
        {
            Type = "progression.level.up",
            SourceEntityId = h.Player.Id
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Status");
        h.Batcher.FlushDirtyVitals();
        dirtied.Should().Contain(h.Player.Id);
    }

    [Fact]
    public void PackageNames_ContainsBothPackages()
    {
        var h = Build();
        h.Handler.PackageNames.Should().Contain("Char.Status").And.Contain("Char.StatusVars");
    }
}
