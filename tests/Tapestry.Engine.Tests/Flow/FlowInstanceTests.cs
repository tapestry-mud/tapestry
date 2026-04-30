using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Flow;

namespace Tapestry.Engine.Tests.Flow;

public class FlowInstanceTests
{
    private static Entity MakeEntity() => new Entity("player", "Rand");

    private static FlowDefinition MakeFlow(params FlowStepDefinition[] steps) => new FlowDefinition
    {
        Id = "test_flow",
        Trigger = "test",
        Steps = steps,
        OnComplete = _ => new FlowCompletionResult(true)
    };

    private static (FlowInstance instance, PlayerSession session, FakeConnection conn) Setup(
        params FlowStepDefinition[] steps)
    {
        var entity = MakeEntity();
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity);
        var def = MakeFlow(steps);
        var instance = new FlowInstance(def, entity);
        return (instance, session, conn);
    }

    // --- InfoStep ---

    [Fact]
    public void InfoStep_auto_advances_and_sends_text()
    {
        var (instance, session, conn) = Setup(
            new InfoStep { Id = "info", Text = _ => "Hello traveler." },
            new ChoiceStep
            {
                Id = "choice",
                Prompt = _ => "Pick one:",
                Options = _ => new[] { new ChoiceOption("Alpha", "a") },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);

        conn.SentText.Should().Contain(s => s.Contains("Hello traveler."));
        conn.SentText.Should().Contain(s => s.Contains("Pick one:"));
    }

    // --- ChoiceStep ---

    [Fact]
    public void ChoiceOption_description_delegate_returns_text_for_entity()
    {
        var entity = MakeEntity();
        var opt = new ChoiceOption("Human", "human", _ => "Adaptable and ambitious.");
        opt.Description!(entity).Should().Be("Adaptable and ambitious.");
    }

    [Fact]
    public void ChoiceStep_accepts_numeric_input()
    {
        object? selected = null;
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[]
                {
                    new ChoiceOption("Human", "human"),
                    new ChoiceOption("Human", "human")
                },
                OnSelect = (_, opt) => { selected = opt.Value; }
            });

        instance.Start(session);
        instance.HandleInput("2");

        selected.Should().Be("human");
    }

    [Fact]
    public void ChoiceStep_accepts_case_insensitive_prefix()
    {
        object? selected = null;
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[] { new ChoiceOption("Human", "human"), new ChoiceOption("Human", "human") },
                OnSelect = (_, opt) => { selected = opt.Value; }
            });

        instance.Start(session);
        instance.HandleInput("hu");

        selected.Should().Be("human");
    }

    [Fact]
    public void ChoiceStep_invalid_input_reprompts_without_advancing()
    {
        var selectCount = 0;
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[] { new ChoiceOption("Human", "human") },
                OnSelect = (_, _) => { selectCount++; }
            });

        instance.Start(session);
        var countBefore = conn.SentText.Count;
        instance.HandleInput("99");

        selectCount.Should().Be(0);
        conn.SentText.Count.Should().BeGreaterThan(countBefore); // re-prompt sent
        instance.CurrentStepIndex.Should().Be(0); // did not advance
    }

    [Fact]
    public void ChoiceStep_out_of_range_number_reprompts()
    {
        var selectCount = 0;
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[] { new ChoiceOption("A", "a") },
                OnSelect = (_, _) => { selectCount++; }
            });

        instance.Start(session);
        instance.HandleInput("5");

        selectCount.Should().Be(0);
    }

    // --- TextStep ---

    [Fact]
    public void TextStep_calls_OnInput_on_valid_input()
    {
        string? received = null;
        var (instance, session, conn) = Setup(
            new TextStep
            {
                Id = "t",
                Prompt = _ => "Enter something:",
                OnInput = (_, val) => { received = val; }
            });

        instance.Start(session);
        instance.HandleInput("hello");

        received.Should().Be("hello");
    }

    [Fact]
    public void TextStep_Validate_failure_sends_InvalidMessage_and_reprompts()
    {
        var (instance, session, conn) = Setup(
            new TextStep
            {
                Id = "t",
                Prompt = _ => "Enter:",
                Validate = s => s.Length >= 3,
                InvalidMessage = "Too short.",
                OnInput = (_, _) => { }
            });

        instance.Start(session);
        instance.HandleInput("ab");

        conn.SentText.Should().Contain(s => s.Contains("Too short."));
        instance.CurrentStepIndex.Should().Be(0);
    }

    // --- ConfirmStep ---

    [Fact]
    public void ConfirmStep_yes_calls_OnYes_and_advances()
    {
        var yesCalled = false;
        var completed = false;
        var (instance, session, conn) = Setup(
            new ConfirmStep
            {
                Id = "q",
                Prompt = _ => "Continue?",
                OnYes = _ => { yesCalled = true; }
            });
        instance.OnCompleted = () => { completed = true; };

        instance.Start(session);
        instance.HandleInput("yes");

        yesCalled.Should().BeTrue();
        completed.Should().BeTrue();
    }

    [Fact]
    public void ConfirmStep_no_calls_OnNo_and_advances()
    {
        var noCalled = false;
        var (instance, session, conn) = Setup(
            new ConfirmStep
            {
                Id = "q",
                Prompt = _ => "Continue?",
                OnNo = _ => { noCalled = true; }
            });

        instance.Start(session);
        instance.HandleInput("n");

        noCalled.Should().BeTrue();
    }

    [Fact]
    public void ConfirmStep_invalid_input_reprompts()
    {
        var (instance, session, conn) = Setup(
            new ConfirmStep { Id = "q", Prompt = _ => "Continue?" });

        instance.Start(session);
        var countBefore = conn.SentText.Count;
        instance.HandleInput("maybe");

        conn.SentText.Count.Should().BeGreaterThan(countBefore);
        instance.CurrentStepIndex.Should().Be(0);
    }

    [Fact]
    public void ChoiceStep_bare_question_mark_shows_hint_message()
    {
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[]
                {
                    new ChoiceOption("Human", "human", _ => "Adaptable and ambitious."),
                    new ChoiceOption("Andoran", "folk", _ => "Children of the Light.")
                },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);
        instance.HandleInput("?");

        conn.SentText.Should().Contain(s => s.Contains("? [option]") || s.Contains("learn more"));
        conn.SentText.Should().NotContain(s => s.Contains("Adaptable and ambitious."));
        instance.CurrentStepIndex.Should().Be(0);
    }

    [Fact]
    public void ChoiceStep_bare_help_shows_hint_message()
    {
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[]
                {
                    new ChoiceOption("Human", "human", _ => "Adaptable and ambitious.")
                },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);
        instance.HandleInput("help");

        conn.SentText.Should().Contain(s => s.Contains("? [option]") || s.Contains("learn more"));
        conn.SentText.Should().NotContain(s => s.Contains("Adaptable and ambitious."));
        instance.CurrentStepIndex.Should().Be(0);
    }

    [Fact]
    public void ChoiceStep_help_with_number_shows_description_for_that_option()
    {
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[]
                {
                    new ChoiceOption("Human", "human", _ => "Adaptable and ambitious."),
                    new ChoiceOption("Andoran", "folk", _ => "Children of the Light.")
                },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);
        instance.HandleInput("? 2");

        conn.SentText.Should().Contain(s => s.Contains("Children of the Light."));
        conn.SentText.Should().NotContain(s => s.Contains("Adaptable and ambitious."));
        instance.CurrentStepIndex.Should().Be(0);
    }

    [Fact]
    public void ChoiceStep_rendered_prompt_includes_help_hint()
    {
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[] { new ChoiceOption("Human", "human") },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);

        conn.SentText.Should().Contain(s => s.Contains("? [option]") || s.Contains("for details"));
    }

    [Fact]
    public void ChoiceStep_help_with_name_shows_matching_description()
    {
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[]
                {
                    new ChoiceOption("Human", "human", _ => "Adaptable and ambitious."),
                    new ChoiceOption("Andoran", "folk", _ => "Children of the Light.")
                },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);
        instance.HandleInput("? Human");

        conn.SentText.Should().Contain(s => s.Contains("Adaptable and ambitious."));
        conn.SentText.Should().NotContain(s => s.Contains("Children of the Light."));
        instance.CurrentStepIndex.Should().Be(0);
    }

    [Fact]
    public void ChoiceStep_help_prefix_is_case_insensitive()
    {
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[]
                {
                    new ChoiceOption("Human", "human", _ => "Adaptable and ambitious.")
                },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);
        instance.HandleInput("HELP HUMAN");

        conn.SentText.Should().Contain(s => s.Contains("Adaptable and ambitious."));
        instance.CurrentStepIndex.Should().Be(0);
    }

    [Fact]
    public void ChoiceStep_help_unknown_option_sends_unknown_message()
    {
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[] { new ChoiceOption("Human", "human", _ => "Warriors.") },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);
        instance.HandleInput("? Elf");

        conn.SentText.Should().Contain(s => s.Contains("Unknown option: Elf"));
        instance.CurrentStepIndex.Should().Be(0);
    }

    [Fact]
    public void ChoiceStep_help_option_without_description_sends_fallback()
    {
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[] { new ChoiceOption("Human", "human") },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);
        instance.HandleInput("? Human");

        conn.SentText.Should().Contain(s => s.Contains("No additional information available for Human."));
        instance.CurrentStepIndex.Should().Be(0);
    }

    // --- SkipIf ---

    [Fact]
    public void SkipIf_true_skips_step()
    {
        var skippedCalled = false;
        var reachedCalled = false;
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "first",
                Prompt = _ => "First:",
                Options = _ => new[] { new ChoiceOption("A", "a") },
                OnSelect = (_, _) => { }
            },
            new ChoiceStep
            {
                Id = "skipped",
                SkipIf = _ => true,
                Prompt = _ => "Skipped:",
                Options = _ => new[] { new ChoiceOption("B", "b") },
                OnSelect = (_, _) => { skippedCalled = true; }
            },
            new ChoiceStep
            {
                Id = "reached",
                Prompt = _ => "Reached:",
                Options = _ => new[] { new ChoiceOption("C", "c") },
                OnSelect = (_, _) => { reachedCalled = true; }
            });

        instance.Start(session);
        instance.HandleInput("1"); // advance past first
        instance.HandleInput("1"); // should be at "reached", not "skipped"

        skippedCalled.Should().BeFalse();
        reachedCalled.Should().BeTrue();
    }

    // --- Completion ---

    [Fact]
    public void All_steps_complete_calls_OnCompleted()
    {
        var completed = false;
        var (instance, session, conn) = Setup(
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Pick:",
                Options = _ => new[] { new ChoiceOption("A", "a") },
                OnSelect = (_, _) => { }
            });
        instance.OnCompleted = () => { completed = true; };

        instance.Start(session);
        instance.HandleInput("1");

        completed.Should().BeTrue();
    }

    // --- ANSI / ClearScreen ---

    [Fact]
    public void FakeConnection_ClearScreen_does_nothing_when_SupportsAnsi_false()
    {
        var conn = new FakeConnection();
        conn.ClearScreen();
        conn.SentText.Should().BeEmpty();
    }

    [Fact]
    public void FakeConnection_ClearScreen_sends_escape_sequence_when_SupportsAnsi_true()
    {
        var conn = new FakeConnection { SupportsAnsi = true };
        conn.ClearScreen();
        conn.SentText.Should().Contain(s => s.Contains("\x1b[2J"));
    }

    // --- Wizard Panel ---

    private static (FlowInstance instance, PlayerSession session, FakeConnection conn) SetupWizard(
        IReadOnlyList<WizardStep> wizardSteps,
        params FlowStepDefinition[] steps)
    {
        var entity = MakeEntity();
        var conn = new FakeConnection { SupportsAnsi = true };
        var session = new PlayerSession(conn, entity);
        var def = new FlowDefinition
        {
            Id = "wizard_flow",
            Trigger = "test",
            Steps = steps,
            OnComplete = _ => new FlowCompletionResult(true),
            WizardSteps = wizardSteps
        };
        var instance = new FlowInstance(def, entity);
        return (instance, session, conn);
    }

    [Fact]
    public void WizardPanel_renders_border_on_ansi_connection()
    {
        var (instance, session, conn) = SetupWizard(
            new[] { new WizardStep("c", "Race") },
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[] { new ChoiceOption("Human", "human") },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);

        conn.SentText.Should().Contain(s => s.Contains("|=============================================|"));
    }

    [Fact]
    public void WizardPanel_renders_progress_row_with_current_step_marked()
    {
        var (instance, session, conn) = SetupWizard(
            new[]
            {
                new WizardStep("race", "Race"),
                new WizardStep("cls", "Class"),
                new WizardStep("align", "Alignment")
            },
            new ChoiceStep
            {
                Id = "race",
                Prompt = _ => "Choose race:",
                Options = _ => new[] { new ChoiceOption("Human", "human") },
                OnSelect = (_, _) => { }
            },
            new ChoiceStep
            {
                Id = "cls",
                Prompt = _ => "Choose class:",
                Options = _ => new[] { new ChoiceOption("Warrior", "warrior") },
                OnSelect = (_, _) => { }
            },
            new ChoiceStep
            {
                Id = "align",
                Prompt = _ => "Choose alignment:",
                Options = _ => new[] { new ChoiceOption("Light", 100) },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);
        conn.SentText.Clear();

        instance.HandleInput("1"); // advance to cls

        conn.SentText.Should().Contain(s => s.Contains("Race [*]"));
        conn.SentText.Should().Contain(s => s.Contains("Class [>]"));
        conn.SentText.Should().Contain(s => s.Contains("Alignment [ ]"));
    }

    [Fact]
    public void WizardPanel_renders_options_with_tag_lines()
    {
        var (instance, session, conn) = SetupWizard(
            new[] { new WizardStep("c", "Race") },
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[]
                {
                    new ChoiceOption("Human", "human", null, "Iron warriors"),
                    new ChoiceOption("Andoran", "folk")
                },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);

        conn.SentText.Should().Contain(s => s.Contains("1. Human - Iron warriors"));
        conn.SentText.Should().Contain(s => s.Contains("2. Andoran"));
    }

    [Fact]
    public void WizardPanel_falls_back_to_plain_render_when_ansi_not_supported()
    {
        var entity = MakeEntity();
        var conn = new FakeConnection { SupportsAnsi = false };
        var session = new PlayerSession(conn, entity);
        var def = new FlowDefinition
        {
            Id = "wf",
            Trigger = "t",
            Steps = new[]
            {
                new ChoiceStep
                {
                    Id = "c",
                    Prompt = _ => "Choose:",
                    Options = _ => new[] { new ChoiceOption("Human", "human") },
                    OnSelect = (_, _) => { }
                }
            },
            OnComplete = _ => new FlowCompletionResult(true),
            WizardSteps = new[] { new WizardStep("c", "Race") }
        };
        var instance = new FlowInstance(def, entity);

        instance.Start(session);

        conn.SentText.Should().NotContain(s => s.Contains("|====="));
        conn.SentText.Should().Contain(s => s.Contains("Choose:"));
    }

    [Fact]
    public void WizardPanel_clears_screen_on_each_render()
    {
        var (instance, session, conn) = SetupWizard(
            new[] { new WizardStep("c", "Race") },
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[] { new ChoiceOption("Human", "human") },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);

        conn.SentText.Should().Contain(s => s.Contains("\x1b[2J"));
    }

    [Fact]
    public void WizardPanel_progress_row_uses_compact_format_when_full_row_overflows()
    {
        var wizardSteps = new[]
        {
            new WizardStep("s1", "Background"),
            new WizardStep("s2", "Heritage"),
            new WizardStep("s3", "Profession"),
            new WizardStep("s4", "Alignment"),
            new WizardStep("s5", "Faction"),
            new WizardStep("s6", "Appearance")
        };

        var steps = wizardSteps.Select(ws => (FlowStepDefinition)new ChoiceStep
        {
            Id = ws.StepId,
            Prompt = _ => $"Choose {ws.Label}:",
            Options = _ => new[] { new ChoiceOption("Option", "opt") },
            OnSelect = (_, _) => { }
        }).ToArray();

        var entity = MakeEntity();
        var conn = new FakeConnection { SupportsAnsi = true };
        var session = new PlayerSession(conn, entity);
        var def = new FlowDefinition
        {
            Id = "long_wizard",
            Trigger = "t",
            Steps = steps,
            OnComplete = _ => new FlowCompletionResult(true),
            WizardSteps = wizardSteps
        };
        var instance = new FlowInstance(def, entity);

        instance.Start(session);
        conn.SentText.Clear();
        instance.HandleInput("1"); // s1 done
        conn.SentText.Clear();
        instance.HandleInput("1"); // s2 done, now at s3 (Profession, wizard index 2)

        conn.SentText.Should().Contain(s => s.Contains("Step 3 of 6: Profession"));
        conn.SentText.Should().NotContain(s => s.Contains("Background [*]   Heritage [*]"));
    }

    [Fact]
    public void WizardPanel_help_query_does_not_clear_screen()
    {
        var (instance, session, conn) = SetupWizard(
            new[] { new WizardStep("c", "Race") },
            new ChoiceStep
            {
                Id = "c",
                Prompt = _ => "Choose:",
                Options = _ => new[]
                {
                    new ChoiceOption("Human", "human", _ => "Adaptable and ambitious.", "Iron warriors")
                },
                OnSelect = (_, _) => { }
            });

        instance.Start(session);
        conn.SentText.Clear();
        instance.HandleInput("? Human");

        conn.SentText.Should().Contain(s => s.Contains("Adaptable and ambitious."));
        conn.SentText.Should().NotContain(s => s.Contains("\x1b[2J"));
    }
}
