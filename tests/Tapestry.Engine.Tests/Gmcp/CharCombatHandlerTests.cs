using FluentAssertions;
using System.Text.Json;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class CharCombatHandlerTests
{
    private record Harness(
        CharCombatHandler Handler,
        FakeGmcpConnectionManager ConnectionManager,
        CombatManager Combat,
        SessionManager Sessions,
        World World,
        EventBus EventBus,
        Entity Player,
        string ConnectionId);

    private static Harness Build()
    {
        var cm = new FakeGmcpConnectionManager();
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        var combat = new CombatManager(world, eb);

        var handler = new CharCombatHandler(cm, sessions, world, eb, combat);

        var entity = new Entity("player", "Hero");
        world.TrackEntity(entity);
        var conn = new FakeConnection();
        sessions.Add(new PlayerSession(conn, entity));
        handler.Configure();

        return new Harness(handler, cm, combat, sessions, world, eb, entity, conn.Id);
    }

    [Fact]
    public void SendBurst_IsNoOp()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        h.ConnectionManager.Sent.Should().BeEmpty();
    }

    [Fact]
    public void CombatEngageEvent_SendsCombatTargetAndTargets()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "combat.engage",
            SourceEntityId = h.Player.Id
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Combat.Target");
        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Combat.Targets");
    }

    [Fact]
    public void CombatEndEvent_SendsCombatTargetWithActiveFalse()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "combat.end",
            SourceEntityId = h.Player.Id
        });

        var sent = h.ConnectionManager.Sent.First(x => x.Package == "Char.Combat.Target");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("active").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void PackageNames_ContainsBothCombatPackages()
    {
        var h = Build();
        h.Handler.PackageNames.Should().Contain("Char.Combat.Target").And.Contain("Char.Combat.Targets");
    }
}
