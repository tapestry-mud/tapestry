using FluentAssertions;
using Tapestry.Engine.Cutscene;

namespace Tapestry.Engine.Tests.Cutscene;

public class CutsceneManagerTests
{
    private static (CutsceneManager Manager, SessionManager Sessions, PlayerSession Session, FakeConnection Conn) Setup()
    {
        var sessions = new SessionManager();
        var manager = new CutsceneManager(sessions);
        var conn = new FakeConnection();
        var entity = new Entity("player", "Nynaeve");
        var session = new PlayerSession(conn, entity) { Phase = LoginPhase.Playing };
        sessions.Add(session);
        return (manager, sessions, session, conn);
    }

    private static List<CutsceneBeat> Beats(params (string Text, int Pause)[] items)
    {
        return items.Select(i => new CutsceneBeat(i.Text, i.Pause)).ToList();
    }

    [Fact]
    public void Play_OpensTheHold_AndEmitsTheFirstBeatOnly()
    {
        var (manager, _, session, conn) = Setup();
        var beats = Beats(("The Weaver bends over the loom.", 10), ("A flash.", 10));

        manager.Play(session.PlayerEntity.Id, beats, skippable: true, currentTick: 0, onComplete: null);

        session.IsPromptHeld.Should().BeTrue();
        conn.SentLines.Should().Contain(l => l.Contains("The Weaver bends over the loom."));
        conn.SentLines.Should().NotContain(l => l.Contains("A flash."));
    }

    [Fact]
    public void AdvanceAll_EmitsOneBeatPerAuthoredCadenceStep_NotBefore()
    {
        var (manager, _, session, conn) = Setup();
        var beats = Beats(("beat0", 5), ("beat1", 5), ("beat2", 0));

        manager.Play(session.PlayerEntity.Id, beats, skippable: false, currentTick: 0, onComplete: null);

        manager.AdvanceAll(4);
        conn.SentLines.Should().NotContain(l => l.Contains("beat1"), "beat1's pauseAfter has not elapsed yet");

        manager.AdvanceAll(5);
        conn.SentLines.Should().Contain(l => l.Contains("beat1"));
        conn.SentLines.Should().NotContain(l => l.Contains("beat2"), "beat2 is gated behind beat1's own cadence");

        manager.AdvanceAll(10);
        conn.SentLines.Should().Contain(l => l.Contains("beat2"));
    }

    [Fact]
    public void NaturalCompletion_ReleasesTheHold_AndFiresOnCompleteExactlyOnce()
    {
        var (manager, _, session, _) = Setup();
        var calls = 0;
        var beats = Beats(("only beat", 0));

        manager.Play(session.PlayerEntity.Id, beats, skippable: true, currentTick: 0, onComplete: () => calls++);

        session.IsPromptHeld.Should().BeFalse("the single beat completed the sequence immediately");
        session.ActiveCutscene.Should().BeNull();
        calls.Should().Be(1);
    }

    [Fact]
    public void Skip_WhenSkippable_FlushesAllRemainingBeats_WithZeroDelay_AndStillPrintsEveryLine()
    {
        var (manager, _, session, conn) = Setup();
        var calls = 0;
        var beats = Beats(("beat0", 100), ("beat1", 100), ("beat2", 100));

        manager.Play(session.PlayerEntity.Id, beats, skippable: true, currentTick: 0, onComplete: () => calls++);
        session.HandleInput("skip");

        conn.SentLines.Should().Contain(l => l.Contains("beat0"));
        conn.SentLines.Should().Contain(l => l.Contains("beat1"));
        conn.SentLines.Should().Contain(l => l.Contains("beat2"));
        session.IsPromptHeld.Should().BeFalse();
        calls.Should().Be(1);
    }

    [Fact]
    public void Skip_And_NaturalCompletion_ProduceIdenticalTerminalState_OnlyFaster()
    {
        var (managerA, _, sessionA, connA) = Setup();
        var naturalCalls = 0;
        managerA.Play(sessionA.PlayerEntity.Id, Beats(("one", 3), ("two", 3), ("three", 0)),
            skippable: true, currentTick: 0, onComplete: () => naturalCalls++);
        managerA.AdvanceAll(3);
        managerA.AdvanceAll(6);

        var (managerB, _, sessionB, connB) = Setup();
        var skipCalls = 0;
        managerB.Play(sessionB.PlayerEntity.Id, Beats(("one", 3), ("two", 3), ("three", 0)),
            skippable: true, currentTick: 0, onComplete: () => skipCalls++);
        sessionB.HandleInput("skip");

        naturalCalls.Should().Be(1);
        skipCalls.Should().Be(1);
        sessionA.IsPromptHeld.Should().BeFalse();
        sessionB.IsPromptHeld.Should().BeFalse();

        foreach (var text in new[] { "one", "two", "three" })
        {
            connA.SentLines.Should().Contain(l => l.Contains(text));
            connB.SentLines.Should().Contain(l => l.Contains(text));
        }
    }

    [Fact]
    public void Skippable_False_SwallowsSkipLine_LikeOrdinaryInput()
    {
        var (manager, _, session, conn) = Setup();
        var beats = Beats(("beat0", 50), ("beat1", 0));

        manager.Play(session.PlayerEntity.Id, beats, skippable: false, currentTick: 0, onComplete: null);
        session.HandleInput("skip");

        conn.SentLines.Should().NotContain(l => l.Contains("beat1"), "skip must be swallowed, not honored");
        session.IsPromptHeld.Should().BeTrue("the cutscene must still be running");
    }

    [Fact]
    public void ActiveCutscene_SwallowsArbitraryInput_WithoutQueueingItAsACommand()
    {
        var (manager, _, session, _) = Setup();
        manager.Play(session.PlayerEntity.Id, Beats(("beat0", 50), ("beat1", 0)), skippable: true, currentTick: 0, onComplete: null);

        session.HandleInput("look");

        session.InputQueueCount.Should().Be(0, "cutscene input is swallowed, not routed as a real command");
    }

    [Fact]
    public void Skippable_True_ShowsTheSkipHint()
    {
        var (manager, _, session, conn) = Setup();
        manager.Play(session.PlayerEntity.Id, Beats(("beat0", 0)), skippable: true, currentTick: 0, onComplete: null);

        conn.SentLines.Should().Contain(l => l.ToLowerInvariant().Contains("skip"));
    }
}
