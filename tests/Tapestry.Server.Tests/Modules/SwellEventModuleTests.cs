using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Server.Modules;
using Tapestry.Server.Tests.Fakes;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Server.Tests.Modules;

public class SwellEventModuleTests
{
    private static (EventBus Bus, PlayerSession Session) Setup()
    {
        var bus = new EventBus();
        var sessions = new SessionManager();
        var module = new SwellEventModule(bus, sessions);
        module.Configure();

        var entity = new Entity("player", "Egwene");
        var session = new PlayerSession(new FakeConnection(), entity) { Phase = LoginPhase.Playing };
        sessions.Add(session);

        return (bus, session);
    }

    private static void PublishRender(EventBus bus, string type, Guid targetId, string text)
    {
        bus.Publish(new GameEvent
        {
            Type = type,
            Data = new Dictionary<string, object?>
            {
                ["targetId"] = targetId.ToString(),
                ["text"] = text
            }
        });
    }

    [Fact]
    public void Telegraph_OpensTheHold_AndRendersText()
    {
        var (bus, session) = Setup();

        PublishRender(bus, "combat.swell.telegraph", session.PlayerEntity.Id, "The warden winds up.");

        session.IsPromptHeld.Should().BeTrue();
        ((FakeConnection)session.Connection).SentLines.Should()
            .ContainSingle(l => l.Contains("The warden winds up."));
    }

    [Fact]
    public void Window_DoesNotChangeHoldState()
    {
        var (bus, session) = Setup();
        PublishRender(bus, "combat.swell.telegraph", session.PlayerEntity.Id, "tell");

        PublishRender(bus, "combat.swell.window", session.PlayerEntity.Id, "An opening!");

        session.IsPromptHeld.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReleasesTheHold_AfterRenderingNarration()
    {
        var (bus, session) = Setup();
        PublishRender(bus, "combat.swell.telegraph", session.PlayerEntity.Id, "tell");

        PublishRender(bus, "combat.swell.resolve", session.PlayerEntity.Id, "You counter the blow.");

        session.IsPromptHeld.Should().BeFalse();
        ((FakeConnection)session.Connection).SentLines.Should()
            .ContainSingle(l => l.Contains("You counter the blow."));
    }

    [Fact]
    public void Abandoned_ReleasesTheHold_WithoutRenderingAnything()
    {
        var (bus, session) = Setup();
        PublishRender(bus, "combat.swell.telegraph", session.PlayerEntity.Id, "tell");
        var sentBefore = ((FakeConnection)session.Connection).SentLines.Count;

        bus.Publish(new GameEvent
        {
            Type = "combat.swell.abandoned",
            Data = new Dictionary<string, object?> { ["targetId"] = session.PlayerEntity.Id.ToString() }
        });

        session.IsPromptHeld.Should().BeFalse();
        ((FakeConnection)session.Connection).SentLines.Should()
            .HaveCount(sentBefore, "abandoning a fight is not player-visible content");
    }
}
