using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Color;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Sustenance;
using Tapestry.Server;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class GmcpServiceItemsTests
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

    private static Entity MakeItem(string name, string? templateId = null, string? rarity = null)
    {
        var item = new Entity("item", name);
        if (templateId != null) { item.SetProperty("template_id", templateId); }
        if (rarity != null) { item.SetProperty(ItemProperties.Rarity, rarity); }
        return item;
    }

    [Fact]
    public void OnPlayerLoggedIn_SendsCharItems()
    {
        var h = Build();

        h.Service.OnPlayerLoggedIn(h.Sessions.GetByEntityId(h.Player.Id)!.Connection.Id, h.Player);

        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Items");
    }

    [Fact]
    public void SendCharItems_EmptyInventory_SendsEmptyList()
    {
        var h = Build();

        h.Service.SendCharItems(h.Player.Id);

        var sent = h.Handler.Sent.Last(x => x.Package == "Char.Items");
        var json = System.Text.Json.JsonSerializer.Serialize(sent.Payload);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void SendCharItems_SingleItem_IncludesNameAndId()
    {
        var h = Build();
        var sword = MakeItem("an iron sword", templateId: "core:iron-sword");
        h.Player.AddToContents(sword);

        h.Service.SendCharItems(h.Player.Id);

        var sent = h.Handler.Sent.Last(x => x.Package == "Char.Items");
        var json = System.Text.Json.JsonSerializer.Serialize(sent.Payload);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void SendCharItems_ItemsWithSameTemplateId_GroupedAsOne()
    {
        var h = Build();
        var potion1 = MakeItem("a cure potion", templateId: "core:cure-light");
        var potion2 = MakeItem("a cure potion", templateId: "core:cure-light");
        h.Player.AddToContents(potion1);
        h.Player.AddToContents(potion2);

        h.Service.SendCharItems(h.Player.Id);

        var sent = h.Handler.Sent.Last(x => x.Package == "Char.Items");
        var json = System.Text.Json.JsonSerializer.Serialize(sent.Payload);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("quantity").GetInt32().Should().Be(2);
    }

    [Fact]
    public void EventItemPickedUp_SendsCharItems()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "entity.item.picked_up",
            SourceEntityId = h.Player.Id,
            TargetEntityId = Guid.NewGuid(),
            Data = { ["itemName"] = "a sword" }
        });

        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Items");
    }

    [Fact]
    public void EventItemDropped_SendsCharItems()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "entity.item.dropped",
            SourceEntityId = h.Player.Id,
            TargetEntityId = Guid.NewGuid(),
            Data = { ["itemName"] = "a sword" }
        });

        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Items");
    }

    [Fact]
    public void EventItemGiven_SendsCharItemsToBothParties()
    {
        var h = Build();
        var recipient = new Entity("player", "Recipient");
        h.World.TrackEntity(recipient);
        var conn2 = new FakeConnection();
        var session2 = new PlayerSession(conn2, recipient);
        h.Sessions.Add(session2);
        var handler2 = new FakeGmcpHandler();
        h.Service.RegisterHandler(conn2.Id, handler2);

        h.EventBus.Publish(new GameEvent
        {
            Type = "entity.item.given",
            SourceEntityId = h.Player.Id,
            TargetEntityId = recipient.Id,
            Data = { ["itemName"] = "a sword" }
        });

        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Items");
        handler2.Sent.Should().Contain(x => x.Package == "Char.Items");
    }
}
