using FluentAssertions;
using System.Text.Json;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Effects;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class CharEffectsHandlerTests
{
    private record Harness(
        CharEffectsHandler Handler,
        FakeGmcpConnectionManager ConnectionManager,
        EffectManager Effects,
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
        var effects = new EffectManager(world, eb);
        var abilities = new AbilityRegistry();

        var handler = new CharEffectsHandler(cm, sessions, world, eb, effects, abilities);

        var entity = new Entity("player", "Hero");
        world.TrackEntity(entity);
        var conn = new FakeConnection();
        sessions.Add(new PlayerSession(conn, entity));
        handler.Configure();

        return new Harness(handler, cm, effects, sessions, eb, entity, conn.Id);
    }

    [Fact]
    public void SendBurst_SendsCharEffectsPackage()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        h.ConnectionManager.Sent.Should().ContainSingle(x => x.Package == "Char.Effects");
    }

    [Fact]
    public void EffectAppliedEvent_SendsCharEffects()
    {
        var h = Build();
        h.Effects.TryApply(new ActiveEffect
        {
            Id = "bless",
            SourceEntityId = h.Player.Id,
            TargetEntityId = h.Player.Id,
            RemainingPulses = 10,
            Flags = new List<string>(),
        });

        h.EventBus.Publish(new GameEvent
        {
            Type = "effect.applied",
            TargetEntityId = h.Player.Id
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "Char.Effects");
    }

    [Fact]
    public void SendBurst_HarmfulFlag_SetsTypeToDebuff()
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

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        var sent = h.ConnectionManager.Sent.First(x => x.Package == "Char.Effects");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("effects")[0].GetProperty("type").GetString().Should().Be("debuff");
    }

    [Fact]
    public void PackageNames_ContainsCharEffects()
    {
        var h = Build();
        h.Handler.PackageNames.Should().Contain("Char.Effects");
    }
}
