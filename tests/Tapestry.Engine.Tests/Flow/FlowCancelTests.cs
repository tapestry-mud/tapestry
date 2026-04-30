using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Tests.Flow;

public class FlowCancelTests
{
    private static FlowDefinition MakeCancellableFlow(bool cancellable = true)
    {
        return new FlowDefinition
        {
            Id = "cancel_test_flow",
            Trigger = "test_trigger",
            Cancellable = cancellable,
            Steps = new[]
            {
                new ChoiceStep
                {
                    Id = "pick",
                    Prompt = _ => "Pick:",
                    Options = _ => new[] { new ChoiceOption("A", "a") },
                    OnSelect = (_, _) => { }
                }
            },
            OnComplete = _ => new FlowCompletionResult(true)
        };
    }

    private static (PlayerSession session, FakeConnection conn) MakeSession()
    {
        var entity = new Entity("player", "Tester");
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity);
        return (session, conn);
    }

    private static FlowInstance StartFlow(PlayerSession session, FlowDefinition definition)
    {
        var instance = new FlowInstance(definition, session.PlayerEntity, new PanelRenderer());
        session.CurrentFlow = instance;
        instance.Start(session);
        return instance;
    }

    [Fact]
    public void Quit_when_cancellable_clears_flow_and_sends_cancel_message()
    {
        var (session, conn) = MakeSession();
        StartFlow(session, MakeCancellableFlow(cancellable: true));

        session.HandleInput("quit");

        session.CurrentFlow.Should().BeNull();
        conn.SentText.Should().Contain(s => s.Contains("Link cancelled."));
    }

    [Fact]
    public void Cancel_when_cancellable_clears_flow_and_sends_cancel_message()
    {
        var (session, conn) = MakeSession();
        StartFlow(session, MakeCancellableFlow(cancellable: true));

        session.HandleInput("cancel");

        session.CurrentFlow.Should().BeNull();
        conn.SentText.Should().Contain(s => s.Contains("Link cancelled."));
    }

    [Fact]
    public void QUIT_uppercase_when_cancellable_clears_flow_and_sends_cancel_message()
    {
        var (session, conn) = MakeSession();
        StartFlow(session, MakeCancellableFlow(cancellable: true));

        session.HandleInput("QUIT");

        session.CurrentFlow.Should().BeNull();
        conn.SentText.Should().Contain(s => s.Contains("Link cancelled."));
    }

    [Fact]
    public void Quit_when_not_cancellable_does_not_clear_flow()
    {
        var (session, conn) = MakeSession();
        var instance = StartFlow(session, MakeCancellableFlow(cancellable: false));

        session.HandleInput("quit");

        session.CurrentFlow.Should().NotBeNull();
        conn.SentText.Should().NotContain(s => s.Contains("Link cancelled."));
    }

    [Fact]
    public void Quit_with_no_active_flow_does_not_throw_and_enqueues_as_normal_command()
    {
        var (session, _) = MakeSession();

        var act = () => session.HandleInput("quit");

        act.Should().NotThrow();
        session.InputQueue.Should().Contain("quit");
    }
}
