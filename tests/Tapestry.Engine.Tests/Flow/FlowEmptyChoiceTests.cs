using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Tests.Flow;

// #20: a choice step that resolves to zero options must surface the missing
// configuration (loud message + "Press Enter to continue.") and abort the flow,
// never render an impossible prompt that the player can't satisfy.
public class FlowEmptyChoiceTests
{
    private static (PlayerSession session, FakeConnection conn) MakeSession()
    {
        var entity = new Entity("player", "Tester");
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity);
        return (session, conn);
    }

    private static FlowDefinition FlowWithEmptyChoice(string? helpHint = "classes")
    {
        return new FlowDefinition
        {
            Id = "empty_choice_flow",
            Trigger = "t",
            Steps = new[]
            {
                new ChoiceStep
                {
                    Id = "class",
                    HelpHint = helpHint,
                    Prompt = _ => "Choose your class:",
                    Options = _ => Array.Empty<ChoiceOption>(),
                    OnSelect = (_, _) => { }
                }
            },
            OnComplete = _ => new FlowCompletionResult(true)
        };
    }

    private static FlowInstance Start(PlayerSession session, FlowDefinition def, Action<string>? onAborted = null)
    {
        var instance = new FlowInstance(def, session.PlayerEntity, new PanelRenderer());
        if (onAborted != null)
        {
            instance.OnAborted = onAborted;
        }
        session.CurrentFlow = instance;
        instance.Start(session);
        return instance;
    }

    [Fact]
    public void EmptyChoice_SurfacesMessageAndPressEnter_NotAnImpossiblePrompt()
    {
        var (session, conn) = MakeSession();
        Start(session, FlowWithEmptyChoice());

        var text = string.Join("\n", conn.SentText);
        text.Should().Contain("classes");      // names the missing thing via HelpHint
        text.Should().Contain("Press Enter");
        // the impossible numbered-option prompt must NOT be rendered
        conn.SentText.Should().NotContain(s => s.TrimStart().StartsWith("1."));
    }

    [Fact]
    public void EmptyChoice_OnEnter_FiresOnAborted_AndIgnoresFurtherInput()
    {
        var (session, conn) = MakeSession();
        string? abortReason = null;
        var instance = Start(session, FlowWithEmptyChoice(), onAborted: r => abortReason = r);

        instance.HandleInput("");          // the acknowledging Enter
        abortReason.Should().NotBeNull();

        // the flow is terminated: further input must not re-prompt or hang
        var before = conn.SentText.Count;
        instance.HandleInput("anything");
        conn.SentText.Count.Should().Be(before);
    }

    [Fact]
    public void EmptyChoice_FallsBackToGenericMessage_WhenNoHelpHint()
    {
        var (session, conn) = MakeSession();
        Start(session, FlowWithEmptyChoice(helpHint: null));
        string.Join("\n", conn.SentText).Should().Contain("no options");
    }

    [Fact]
    public void NonEmptyChoice_StillRendersOptionsNormally()
    {
        var (session, conn) = MakeSession();
        var def = new FlowDefinition
        {
            Id = "f",
            Trigger = "t",
            Steps = new[]
            {
                new ChoiceStep
                {
                    Id = "pick",
                    Prompt = _ => "Pick:",
                    Options = _ => new[] { new ChoiceOption("Alpha", "a") },
                    OnSelect = (_, _) => { }
                }
            },
            OnComplete = _ => new FlowCompletionResult(true)
        };
        Start(session, def);
        conn.SentText.Should().Contain(s => s.Contains("1.") && s.Contains("Alpha"));
    }
}
