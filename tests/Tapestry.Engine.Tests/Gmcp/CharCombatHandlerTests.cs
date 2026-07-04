using FluentAssertions;
using System.Collections.Generic;
using System.Text.Json;
using Tapestry.Shared;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Stats;
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
        var combat = new CombatManager(world, eb, vitalsService: new VitalsService(eb));

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
    public void SwellResolveEvent_RefreshesPlayerCombatTarget()
    {
        var h = Build();

        var room = new Room("test:arena", "Arena", "A test arena.");
        h.World.AddRoom(room);
        h.Player.Stats.BaseMaxHp = 100;
        h.Player.Stats.SetVital(VitalKind.Hp, 100);
        room.AddEntity(h.Player);

        var boss = new Entity("npc", "Boss");
        boss.AddTag("npc");
        boss.Stats.BaseMaxHp = 100;
        boss.Stats.SetVital(VitalKind.Hp, 100);
        boss.SetProperty("level", 5);
        room.AddEntity(boss);
        h.World.TrackEntity(boss);

        h.Combat.Engage(h.Player, boss);
        h.ConnectionManager.Sent.Clear(); // discard the engage refresh; we want the swell refresh

        // The swell resolve event carries the player id in Data["targetId"]; the boss took the chunk.
        h.EventBus.Publish(new GameEvent
        {
            Type = "combat.swell.resolve",
            SourceEntityId = boss.Id,
            Data = new Dictionary<string, object?>
            {
                ["targetId"] = h.Player.Id.ToString(),
                ["text"] = "You counter the blow."
            }
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Combat.Target");
        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Combat.Targets");
    }

    [Fact]
    public void PackageNames_ContainsBothCombatPackages()
    {
        var h = Build();
        h.Handler.PackageNames.Should().Contain("Char.Combat.Target").And.Contain("Char.Combat.Targets");
    }
}
