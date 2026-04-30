using FluentAssertions;
using System.Text.Json;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Color;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Sustenance;
using Tapestry.Server;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class GmcpServiceCombatTargetTests
{
    private class FakeGmcpHandler : IGmcpHandler
    {
        public bool GmcpActive { get; set; } = true;
        public List<(string Package, object Payload)> Sent { get; } = new();
        public Action<string, JsonElement>? OnGmcpMessage { get; set; }

        public void Send(string package, object payload)
        {
            Sent.Add((package, payload));
        }

        public bool SupportsPackage(string package) => true;
    }

    private record Harness(
        GmcpService Service,
        CombatManager Combat,
        SessionManager Sessions,
        World World,
        EventBus EventBus,
        FakeGmcpHandler Handler,
        Entity Player);

    private static Harness Build()
    {
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        var combatManager = new CombatManager(world, eb);
        var svc = new GmcpService(
            sessions, world, eb,
            new GameClock(eb, new ServerConfig()),
            new WeatherService(new AreaRegistry(), new WeatherZoneRegistry(), world, sessions, eb, new ServerConfig()),
            new ProgressionManager(world, eb),
            new AlignmentManager(world, eb, new AlignmentConfig()),
            new SustenanceConfig(),
            new CommandRegistry(),
            new EffectManager(world, eb),
            combatManager,
            new AbilityRegistry(),
            new ThemeRegistry(),
            new RarityRegistry(),
            new EssenceRegistry(), new SlotRegistry());

        var player = new Entity("player", "TestPlayer");
        player.Stats.BaseMaxHp = 100;
        player.Stats.Hp = 100;
        world.TrackEntity(player);
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, player);
        sessions.Add(session);

        var handler = new FakeGmcpHandler();
        svc.RegisterHandler(conn.Id, handler);

        return new Harness(svc, combatManager, sessions, world, eb, handler, player);
    }

    [Fact]
    public void SendCharCombatTarget_NoTarget_SendsInactive()
    {
        var h = Build();

        h.Service.SendCharCombatTarget(h.Player.Id);

        var sent = h.Handler.Sent.First(x => x.Package == "Char.Combat.Target");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("active").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void SendCharCombatTarget_WithTarget_SendsActiveWithHealthTier()
    {
        var h = Build();
        var mob = new Entity("mob", "Goblin");
        mob.Stats.BaseMaxHp = 100;
        mob.Stats.Hp = 50;
        mob.AddTag("killable");
        h.World.TrackEntity(mob);
        h.Combat.Engage(h.Player, mob);

        h.Service.SendCharCombatTarget(h.Player.Id);

        var sent = h.Handler.Sent.First(x => x.Package == "Char.Combat.Target");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("active").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("name").GetString().Should().Be("Goblin");
        doc.RootElement.GetProperty("healthTier").GetString().Should().Be("small wounds");
        doc.RootElement.GetProperty("healthText").GetString().Should().Be("has some small wounds");
    }

    [Fact]
    public void SendCharCombatTarget_PerfectHealth_SendsCorrectTier()
    {
        var h = Build();
        var mob = new Entity("mob", "Troll");
        mob.Stats.BaseMaxHp = 100;
        mob.Stats.Hp = 100;
        mob.AddTag("killable");
        h.World.TrackEntity(mob);
        h.Combat.Engage(h.Player, mob);

        h.Service.SendCharCombatTarget(h.Player.Id);

        var sent = h.Handler.Sent.First(x => x.Package == "Char.Combat.Target");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("healthTier").GetString().Should().Be("perfect");
    }
}
