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

public class GmcpServiceDisplayColorsTests
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
        ThemeRegistry Theme,
        SessionManager Sessions,
        FakeGmcpHandler Handler,
        Entity Player);

    private static Harness Build(ThemeRegistry? themeOverride = null)
    {
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        var theme = themeOverride ?? new ThemeRegistry();

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
            theme,
            new RarityRegistry(),
            new EssenceRegistry(), new SlotRegistry());

        var player = new Entity("player", "TestPlayer");
        world.TrackEntity(player);
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, player);
        sessions.Add(session);

        var handler = new FakeGmcpHandler();
        svc.RegisterHandler(conn.Id, handler);

        return new Harness(svc, theme, sessions, handler, player);
    }

    [Fact]
    public void OnPlayerLoggedIn_SendsWorldDisplayColors()
    {
        var h = Build();

        h.Service.OnPlayerLoggedIn(h.Sessions.GetByEntityId(h.Player.Id)!.Connection.Id, h.Player);

        h.Handler.Sent.Should().Contain(x => x.Package == "World.Display.Colors");
    }

    [Fact]
    public void SendWorldDisplayColors_IncludesRegisteredHtmlEntries()
    {
        var theme = new ThemeRegistry();
        theme.Register("item.rare", new ThemeEntry { Fg = "green", Html = "text-green-400" });
        theme.Register("npc", new ThemeEntry { Fg = "yellow" }); // no html -- excluded
        var h = Build(theme);

        h.Service.OnPlayerLoggedIn(h.Sessions.GetByEntityId(h.Player.Id)!.Connection.Id, h.Player);

        var sent = h.Handler.Sent.First(x => x.Package == "World.Display.Colors");
        var json = System.Text.Json.JsonSerializer.Serialize(sent.Payload);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var colors = doc.RootElement.GetProperty("colors");
        colors.GetProperty("item.rare").GetString().Should().Be("text-green-400");
        colors.TryGetProperty("npc", out _).Should().BeFalse();
    }
}
