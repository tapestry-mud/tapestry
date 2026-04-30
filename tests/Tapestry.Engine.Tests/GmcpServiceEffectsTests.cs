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

public class GmcpServiceEffectsTests
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
        EffectManager Effects,
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
        var effectManager = new EffectManager(world, eb);
        var svc = new GmcpService(
            sessions, world, eb,
            new GameClock(eb, new ServerConfig()),
            new WeatherService(new AreaRegistry(), new WeatherZoneRegistry(), world, sessions, eb, new ServerConfig()),
            new ProgressionManager(world, eb),
            new AlignmentManager(world, eb, new AlignmentConfig()),
            new SustenanceConfig(),
            new CommandRegistry(),
            effectManager,
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

        return new Harness(svc, effectManager, sessions, world, eb, handler, player);
    }

    [Fact]
    public void OnPlayerLoggedIn_SendsCharEffectsPackage()
    {
        var h = Build();

        h.Service.OnPlayerLoggedIn(h.Sessions.GetByEntityId(h.Player.Id)!.Connection.Id, h.Player);

        h.Handler.Sent.Should().Contain(x => x.Package == "Char.Effects");
    }

    [Fact]
    public void OnPlayerLoggedIn_CharEffects_HasEmptyEffectsListWhenNoEffectsActive()
    {
        var h = Build();

        h.Service.OnPlayerLoggedIn(h.Sessions.GetByEntityId(h.Player.Id)!.Connection.Id, h.Player);

        var sent = h.Handler.Sent.First(x => x.Package == "Char.Effects");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("effects").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void SendCharEffects_IncludesActiveEffect_WithCorrectShape()
    {
        var h = Build();
        h.Effects.TryApply(new ActiveEffect
        {
            Id = "test-effect",
            SourceAbilityId = "bless",
            SourceEntityId = h.Player.Id,
            TargetEntityId = h.Player.Id,
            RemainingPulses = 10,
            Flags = new List<string> { "magic" },
        });

        h.Service.SendCharEffects(h.Player.Id);

        var sent = h.Handler.Sent.First(x => x.Package == "Char.Effects");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        var effect = doc.RootElement.GetProperty("effects")[0];
        effect.GetProperty("id").GetString().Should().Be("test-effect");
        effect.GetProperty("name").GetString().Should().Be("bless");
        effect.GetProperty("remainingPulses").GetInt32().Should().Be(10);
        effect.GetProperty("type").GetString().Should().Be("buff");
    }

    [Fact]
    public void SendCharEffects_HarmfulFlag_SetsTypeToDebuff()
    {
        var h = Build();
        h.Effects.TryApply(new ActiveEffect
        {
            Id = "poison",
            SourceEntityId = h.Player.Id,
            TargetEntityId = h.Player.Id,
            RemainingPulses = 5,
            Flags = new List<string> { "harmful" },
        });

        h.Service.SendCharEffects(h.Player.Id);

        var sent = h.Handler.Sent.First(x => x.Package == "Char.Effects");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        var effect = doc.RootElement.GetProperty("effects")[0];
        effect.GetProperty("type").GetString().Should().Be("debuff");
    }

    [Fact]
    public void SendCharEffects_FallsBackToIdWhenSourceAbilityIdIsNull()
    {
        var h = Build();
        h.Effects.TryApply(new ActiveEffect
        {
            Id = "raw-effect-id",
            SourceEntityId = h.Player.Id,
            TargetEntityId = h.Player.Id,
            RemainingPulses = 3,
            Flags = new List<string>(),
        });

        h.Service.SendCharEffects(h.Player.Id);

        var sent = h.Handler.Sent.First(x => x.Package == "Char.Effects");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("effects")[0].GetProperty("name").GetString().Should().Be("raw-effect-id");
    }
}
