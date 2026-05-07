using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class PlayerSessionPromptModeTests
{
    private static (PlayerSession session, FakeConnection conn) Make()
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "Testplayer");
        var session = new PlayerSession(conn, entity);
        return (session, conn);
    }

    [Fact]
    public void HandleInput_NormalMode_EnqueuesInput()
    {
        var (session, conn) = Make();
        session.InputMode = InputMode.Normal;

        conn.SimulateInput("look");

        session.TryDequeueInput(out var input).Should().BeTrue();
        input.Should().Be("look");
        session.TryDequeueInput(out _).Should().BeFalse();
    }

    [Fact]
    public void HandleInput_PromptMode_CallsPromptHandler_NotQueue()
    {
        var (session, conn) = Make();
        var received = new List<string>();
        session.InputMode = InputMode.Prompt;
        session.PromptHandler = (input) => { received.Add(input); };

        conn.SimulateInput("secret");

        received.Should().ContainSingle().Which.Should().Be("secret");
        session.TryDequeueInput(out _).Should().BeFalse();
    }

    [Fact]
    public void HandleInput_PromptMode_NullHandler_DropsInput()
    {
        var (session, conn) = Make();
        session.InputMode = InputMode.Prompt;
        session.PromptHandler = null;

        var act = () => conn.SimulateInput("secret");

        act.Should().NotThrow();
        session.TryDequeueInput(out _).Should().BeFalse();
    }

    [Fact]
    public void HandleInput_PromptMode_TakesPriorityOverCurrentFlow()
    {
        var (session, conn) = Make();
        var promptReceived = new List<string>();
        session.InputMode = InputMode.Prompt;
        session.PromptHandler = (input) => { promptReceived.Add(input); };

        // Even if CurrentFlow were set, prompt mode should win.
        // (CurrentFlow can't be set via public API without a full FlowEngine,
        //  so this test verifies queue stays empty -- the flow path is not taken.)
        conn.SimulateInput("anything");

        promptReceived.Should().ContainSingle();
        session.TryDequeueInput(out _).Should().BeFalse();
    }

    [Fact]
    public void DefaultInputMode_IsNormal()
    {
        var (session, _) = Make();
        session.InputMode.Should().Be(InputMode.Normal);
    }
}
