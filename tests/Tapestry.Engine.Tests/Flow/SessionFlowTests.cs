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
        session.Phase = SessionPhase.Playing;

        session.HandleInput("look");

        session.InputQueue.Should().Contain("look");
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
                    Prompt = _ => "Pick:",
                    Options = _ => new[] { new ChoiceOption("X", "x") },
                    OnSelect = (_, opt) => { received.Add(opt.Value?.ToString() ?? ""); }
                }
            },
            OnComplete = _ => new FlowCompletionResult(true)
        };

        var instance = new FlowInstance(def, entity);
        instance.OnCompleted = () => { };
        session.CurrentFlow = instance;
        instance.Start(session);

        session.HandleInput("1");

        received.Should().Contain("x");
        session.InputQueue.Should().BeEmpty();
    }

    [Fact]
    public void Connection_OnInput_routes_through_HandleInput()
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "Mat");
        var session = new PlayerSession(conn, entity);
        session.Phase = SessionPhase.Playing;

        conn.SimulateInput("look");

        session.InputQueue.Should().Contain("look");
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
            Phase = SessionPhase.Creating,
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
            Phase = SessionPhase.Playing,
            NeedsPromptRefresh = true
        };
        sessions.Add(session);

        var renderer = new PromptRenderer();
        sessions.FlushPrompts(renderer);

        session.PromptDisplayed.Should().BeTrue();
    }
}
