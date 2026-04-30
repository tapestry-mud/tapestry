using FluentAssertions;
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

public class GmcpServiceEquipmentTests
{
    private class FakeGmcpHandler : IGmcpHandler
    {
        public bool GmcpActive { get; set; } = true;
        public List<(string Package, object Payload)> Sent { get; } = new();
        public Action<string, System.Text.Json.JsonElement>? OnGmcpMessage { get; set; }

        public void Send(string package, object payload)
        {
            Sent.Add((package, payload));
        }

        public bool SupportsPackage(string package) => true;
    }

    private record Harness(
        GmcpService Service,
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

        var svc = new GmcpService(
            sessions, world, eb,
            new GameClock(eb, new ServerConfig()),
            new WeatherService(new AreaRegistry(), new WeatherZoneRegistry(), world, sessions, eb, new ServerConfig()),
            new ProgressionManager(world, eb),
            new AlignmentManager(world, eb, new AlignmentConfig()),
            new SustenanceConfig(),
            new CommandRegistry(),
            new EffectManager(world, eb),
            new CombatManager(world, eb),
            new AbilityRegistry(),
            new ThemeRegistry(),
            new RarityRegistry(),
            new EssenceRegistry(), new SlotRegistry());

        var player = new Entity("player", "TestPlayer");
        world.TrackEntity(player);
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, player);
        sessions.Add(session);

        var handler = new FakeGmcpHandler();
        svc.RegisterHandler(conn.Id, handler);

        return new Harness(svc, sessions, world, eb, handler, player);
    }

    [Fact]
    public void OnPlayerLoggedIn_SendsCharEquipment()
    {
        var h = Build();

        h.Service.OnPlayerLoggedIn(h.Sessions.GetByEntityId(h.Player.Id)!.Connection.Id, h.Player);

        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Equipment");
    }

    [Fact]
    public void SendCharEquipment_NoEquipment_SendsEmptySlots()
    {
        var h = Build();

        h.Service.SendCharEquipment(h.Player.Id);

        var sent = h.Handler.Sent.Last(x => x.Package == "Char.Equipment");
        var json = System.Text.Json.JsonSerializer.Serialize(sent.Payload);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("slots").EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public void SendCharEquipment_WithEquippedItem_IncludesSlotData()
    {
        var h = Build();
        var helm = new Entity("item", "a leather helm");
        h.Player.SetEquipment("head", helm);

        h.Service.SendCharEquipment(h.Player.Id);

        var sent = h.Handler.Sent.Last(x => x.Package == "Char.Equipment");
        var json = System.Text.Json.JsonSerializer.Serialize(sent.Payload);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("slots").TryGetProperty("head", out _).Should().BeTrue();
    }

    [Fact]
    public void EventEntityEquipped_SendsCharEquipment()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "entity.equipped",
            SourceEntityId = h.Player.Id,
            Data = { ["slot"] = "head", ["itemId"] = Guid.NewGuid().ToString() }
        });

        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Equipment");
    }

    [Fact]
    public void EventEntityEquipped_AlsoSendsCharItems()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "entity.equipped",
            SourceEntityId = h.Player.Id,
            Data = { ["slot"] = "head", ["itemId"] = Guid.NewGuid().ToString() }
        });

        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Items");
    }

    [Fact]
    public void EventEntityUnequipped_SendsCharEquipmentAndCharItems()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "entity.unequipped",
            SourceEntityId = h.Player.Id,
            Data = { ["slot"] = "head", ["itemId"] = Guid.NewGuid().ToString() }
        });

        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Equipment");
        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Items");
    }
}
