using FluentAssertions;
using System.Text.Json;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class CharVitalsHandlerTests
{
    private record Harness(
        CharVitalsHandler Handler,
        FakeGmcpConnectionManager ConnectionManager,
        DirtyVitalsBatcher Batcher,
        SessionManager Sessions,
        World World,
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

        var handler = new CharVitalsHandler(cm, batcher, sessions, world, eb);

        var entity = new Entity("player", "TestPlayer");
        entity.Stats.Hp = 80;
        entity.Stats.MaxHp = 100;
        entity.Stats.Resource = 40;
        entity.Stats.MaxResource = 50;
        entity.Stats.Movement = 90;
        entity.Stats.MaxMovement = 100;
        world.TrackEntity(entity);

        var conn = new FakeConnection();
        sessions.Add(new PlayerSession(conn, entity));

        handler.Configure();

        return new Harness(handler, cm, batcher, sessions, world, eb, entity, conn.Id);
    }

    [Fact]
    public void SendBurst_SendsCharVitalsWithEntityStats()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        var sent = h.ConnectionManager.Sent.Should().ContainSingle(x => x.Package == "Char.Vitals").Subject;
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("hp").GetInt32().Should().Be(80);
        doc.RootElement.GetProperty("maxhp").GetInt32().Should().Be(100);
    }

    [Fact]
    public void Configure_RegistersFlushCallback_SoFlushSendsVitals()
    {
        var h = Build();

        h.Batcher.MarkDirty(h.Player.Id);
        h.Batcher.FlushDirtyVitals();

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Vitals");
    }

    [Fact]
    public void AbilityUsedEvent_MarksVitalsDirty_FlushSendsVitals()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "ability.used",
            SourceEntityId = h.Player.Id
        });
        h.Batcher.FlushDirtyVitals();

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Vitals");
    }

    [Fact]
    public void PackageNames_ContainsCharVitals()
    {
        var h = Build();
        h.Handler.PackageNames.Should().Contain("Char.Vitals");
    }
}
