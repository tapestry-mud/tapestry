using FluentAssertions;
using System.Text.Json;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Progression;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class CharExperienceHandlerTests
{
    private record Harness(
        CharExperienceHandler Handler,
        FakeGmcpConnectionManager ConnectionManager,
        SessionManager Sessions,
        EventBus EventBus,
        Entity Player,
        string ConnectionId);

    private static Harness Build()
    {
        var cm = new FakeGmcpConnectionManager();
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        var progression = new ProgressionManager(world, eb);

        var handler = new CharExperienceHandler(cm, sessions, world, eb, progression);

        var entity = new Entity("player", "Hero");
        world.TrackEntity(entity);
        var conn = new FakeConnection();
        sessions.Add(new PlayerSession(conn, entity));
        handler.Configure();

        return new Harness(handler, cm, sessions, eb, entity, conn.Id);
    }

    [Fact]
    public void SendBurst_SendsCharExperiencePackage()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        h.ConnectionManager.Sent.Should().ContainSingle(x => x.Package == "Char.Experience");
    }

    [Fact]
    public void SendBurst_CharExperience_ContainsTracksArray()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        var sent = h.ConnectionManager.Sent.First(x => x.Package == "Char.Experience");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("tracks").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public void XpGainedEvent_SendsCharExperience()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "progression.xp.gained",
            SourceEntityId = h.Player.Id
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Experience");
    }

    [Fact]
    public void LevelUpEvent_SendsCharExperience()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "progression.level.up",
            SourceEntityId = h.Player.Id
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Experience");
    }

    [Fact]
    public void PackageNames_ContainsCharExperience()
    {
        var h = Build();
        h.Handler.PackageNames.Should().Contain("Char.Experience");
    }
}
