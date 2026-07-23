using FluentAssertions;
using Tapestry.Engine.Cutscene;
using Tapestry.Engine.Heartbeat;

namespace Tapestry.Engine.Tests.Heartbeat;

public class CutscenePulseTests
{
    [Fact]
    public void HasCadence1AndPriority90_SameTierAsSwellClockPulse()
    {
        var sessions = new SessionManager();
        var manager = new CutsceneManager(sessions);
        var pulse = new CutscenePulse(manager);

        Assert.Equal(1, pulse.Cadence);
        Assert.Equal(90, pulse.Priority);
    }

    [Fact]
    public void Execute_AdvancesTheCutsceneClock()
    {
        var sessions = new SessionManager();
        var manager = new CutsceneManager(sessions);
        var conn = new FakeConnection();
        var entity = new Entity("player", "Moiraine");
        var session = new PlayerSession(conn, entity) { Phase = LoginPhase.Playing };
        sessions.Add(session);

        manager.Play(entity.Id,
            new List<CutsceneBeat> { new("beat0", 1), new("beat1", 0) },
            skippable: false, currentTick: 0, onComplete: null);

        var pulse = new CutscenePulse(manager);
        pulse.Execute(new PulseContext { CurrentTick = 0, World = new World(), EventBus = new EventBus() });
        conn.SentLines.Should().NotContain(l => l.Contains("beat1"));

        pulse.Execute(new PulseContext { CurrentTick = 1, World = new World(), EventBus = new EventBus() });
        conn.SentLines.Should().Contain(l => l.Contains("beat1"));
    }
}
