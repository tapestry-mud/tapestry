using FluentAssertions;
using System.Text.Json;
using Tapestry.Engine;
using Tapestry.Data;
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

public class GmcpServiceLoginPhaseTests
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

    private static GmcpService MakeService(out SessionManager sessions)
    {
        sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        return new GmcpService(
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
    }

    [Fact]
    public void SendLoginPhase_SendsCorrectPackageToRegisteredHandler()
    {
        var svc = MakeService(out _);
        var handler = new FakeGmcpHandler();
        svc.RegisterHandler("conn-1", handler);

        svc.SendLoginPhase("conn-1", "name");

        handler.Sent.Should().ContainSingle();
        handler.Sent[0].Package.Should().Be("Char.Login.Phase");
        var json = System.Text.Json.JsonSerializer.Serialize(handler.Sent[0].Payload);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("phase").GetString().Should().Be("name");
    }

    [Fact]
    public void SendLoginPhase_NoHandler_DoesNotThrow()
    {
        var svc = MakeService(out _);

        var act = () => svc.SendLoginPhase("nonexistent", "name");

        act.Should().NotThrow();
    }

    [Fact]
    public void SendLoginPhase_GmcpNotActive_DoesNotSend()
    {
        var svc = MakeService(out _);
        var handler = new FakeGmcpHandler { GmcpActive = false };
        svc.RegisterHandler("conn-1", handler);

        svc.SendLoginPhase("conn-1", "password");

        handler.Sent.Should().BeEmpty();
    }

    [Fact]
    public void SendLoginPhase_AfterUnregister_DoesNotThrow()
    {
        var svc = MakeService(out _);
        var handler = new FakeGmcpHandler();
        svc.RegisterHandler("conn-1", handler);
        svc.UnregisterHandler("conn-1");

        var act = () => svc.SendLoginPhase("conn-1", "playing");

        act.Should().NotThrow();
        handler.Sent.Should().BeEmpty();
    }
}
