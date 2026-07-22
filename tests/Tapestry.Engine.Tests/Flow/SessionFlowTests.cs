using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Prompt;

namespace Tapestry.Engine.Tests.Flow;

public class SessionFlowTests
{
    [Fact]
    public void HandleInput_with_no_flow_enqueues_to_InputQueue()
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "Mat");
        var session = new PlayerSession(conn, entity);
        session.Phase = LoginPhase.Playing;

        session.HandleInput("look");

        session.TryDequeueInput(out var dequeued).Should().BeTrue();
        dequeued.Should().Be("look");
    }

    [Fact]
    public void HandleInput_with_active_flow_dispatches_to_flow_not_queue()
    {
        var received = new List<string>();
        var conn = new FakeConnection();
        var entity = new Entity("player", "Mat");
        var session = new PlayerSession(conn, entity);

        var def = new FlowDefinition
        {
            Id = "test",
            Trigger = "t",
            Steps = new[]
            {
                new ChoiceStep
                {
                    Id = "c",
                    Prompt = (_, _) => "Pick:",
                    Options = (_, _) => new[] { new ChoiceOption("X", "x") },
                    OnSelect = (_, _, opt) => { received.Add(opt.Value?.ToString() ?? ""); }
                }
            },
            OnComplete = (_, _) => new FlowCompletionResult(true)
        };

        var instance = new FlowInstance(def, entity);
        instance.OnCompleted = () => { };
        session.CurrentFlow = instance;
        instance.Start(session);

        session.HandleInput("1");

        received.Should().Contain("x");
        session.TryDequeueInput(out _).Should().BeFalse();
    }

    [Fact]
    public void Connection_OnInput_routes_through_HandleInput()
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "Mat");
        var session = new PlayerSession(conn, entity);
        session.Phase = LoginPhase.Playing;

        conn.SimulateInput("look");

        session.TryDequeueInput(out var routed).Should().BeTrue();
        routed.Should().Be("look");
    }

    [Fact]
    public void FlushPrompts_skips_sessions_in_Creating_phase()
    {
        var sessions = new SessionManager();
        var conn = new FakeConnection();
        var entity = new Entity("player", "Egwene");
        entity.SetProperty("prompt_template", "{hp}hp>");

        var session = new PlayerSession(conn, entity)
        {
            Phase = LoginPhase.Creating,
            NeedsPromptRefresh = true
        };
        sessions.Add(session);

        var renderer = new PromptRenderer();
        sessions.FlushPrompts(renderer);

        session.PromptDisplayed.Should().BeFalse();
    }

    [Fact]
    public void FlushPrompts_renders_prompt_for_Playing_sessions()
    {
        var sessions = new SessionManager();
        var conn = new FakeConnection();
        var entity = new Entity("player", "Nynaeve");
        entity.SetProperty("prompt_template", ">");

        var session = new PlayerSession(conn, entity)
        {
            Phase = LoginPhase.Playing,
            NeedsPromptRefresh = true
        };
        sessions.Add(session);

        var renderer = new PromptRenderer();
        sessions.FlushPrompts(renderer);

        session.PromptDisplayed.Should().BeTrue();
    }

    [Fact]
    public void FlushPrompts_skips_held_session_regardless_of_pending_content_sends()
    {
        var sessions = new SessionManager();
        var conn = new FakeConnection();
        var entity = new Entity("player", "Perrin");
        entity.SetProperty("prompt_template", ">");

        var session = new PlayerSession(conn, entity)
        {
            Phase = LoginPhase.Playing
        };
        sessions.Add(session);
        session.OpenPromptHold("swell");

        sessions.SendToPlayer(entity.Id, "one\r\n");
        sessions.SendToPlayer(entity.Id, "two\r\n");
        sessions.SendToPlayer(entity.Id, "three\r\n");

        var renderer = new PromptRenderer();
        sessions.FlushPrompts(renderer);

        session.PromptDisplayed.Should().BeFalse();
        session.NeedsPromptRefresh.Should().BeTrue("held sessions must not consume the arm");
    }

    [Fact]
    public void FlushPrompts_releasingHold_withNoTrailingContent_stillRendersExactlyOnePrompt()
    {
        var sessions = new SessionManager();
        var conn = new FakeConnection();
        var entity = new Entity("player", "Perrin");
        entity.SetProperty("prompt_template", ">");

        var session = new PlayerSession(conn, entity)
        {
            Phase = LoginPhase.Playing
        };
        sessions.Add(session);
        session.OpenPromptHold("swell");

        var renderer = new PromptRenderer();
        sessions.FlushPrompts(renderer);
        session.PromptDisplayed.Should().BeFalse();
        conn.SentLines.Should().BeEmpty();

        session.ReleasePromptHold("swell");
        sessions.FlushPrompts(renderer);

        session.PromptDisplayed.Should().BeTrue();
        conn.SentLines.Should().HaveCount(1);
    }
}
