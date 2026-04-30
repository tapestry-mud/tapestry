using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Races;
using Tapestry.Engine.Ui;
using Tapestry.Scripting;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests.Modules;

public class FlowsModuleTests
{
    private class FakePersistence : IFlowPersistence
    {
        public bool PlayerExists(string name) => false;
        public void SaveNewPlayer(Entity entity, string passwordHash) { }
    }

    private static (JintRuntime runtime, FlowRegistry registry) CreateRuntime()
    {
        var registry = new FlowRegistry();
        var sessions = new SessionManager();
        var playerCreator = new PlayerCreator();
        var world = new World(playerCreator);
        var persistence = new FakePersistence();
        var eventBus = new EventBus();
        var engine = new FlowEngine(registry, sessions, world, persistence, new PanelRenderer(),
            new ClassRegistry(), new RaceRegistry(), new AlignmentManager(world, eventBus, new AlignmentConfig()),
            playerCreator, eventBus);
        var module = new FlowsModule(registry, engine, sessions);
        var runtime = new JintRuntime(new IJintApiModule[] { module }, NullLogger<JintRuntime>.Instance);
        return (runtime, registry);
    }

    [Fact]
    public void Register_info_flow_from_JS_adds_to_registry()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "test_flow",
                trigger: "new_player_connect",
                steps: [
                    { id: "welcome", type: "info", text: "Hello!" }
                ],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        registry.Get("test_flow").Should().NotBeNull();
        registry.Get("test_flow")!.Trigger.Should().Be("new_player_connect");
    }

    [Fact]
    public void Register_flow_with_choice_step()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "choice_flow",
                trigger: "t",
                steps: [
                    {
                        id: "pick",
                        type: "choice",
                        prompt: "Choose:",
                        options: [{ label: "Alpha", value: "alpha" }],
                        on_select: (entity, option) => {}
                    }
                ],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        var def = registry.Get("choice_flow")!;
        def.Steps.Should().HaveCount(1);
        def.Steps[0].Should().BeOfType<ChoiceStep>();
    }

    [Fact]
    public void Register_flow_on_complete_returning_false_propagates_message()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "reject_flow",
                trigger: "t",
                steps: [],
                on_complete: (entity) => ({ success: false, message: "Not allowed." })
            });
            """, "test");

        var def = registry.Get("reject_flow")!;
        var entity = new Entity("player", "Test");
        var result = def.OnComplete(entity);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Not allowed.");
    }

    [Fact]
    public void Register_flow_with_skip_if()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "skip_flow",
                trigger: "t",
                steps: [
                    {
                        id: "s",
                        type: "choice",
                        skip_if: (entity) => true,
                        prompt: "Skip me:",
                        options: [{ label: "A", value: "a" }],
                        on_select: (entity, option) => {}
                    }
                ],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        var def = registry.Get("skip_flow")!;
        var entity = new Entity("player", "Test");
        def.Steps[0].SkipIf!(entity).Should().BeTrue();
    }

    [Fact]
    public void Register_flow_with_dynamic_options_function()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "dyn_flow",
                trigger: "t",
                steps: [
                    {
                        id: "pick",
                        type: "choice",
                        prompt: "Choose:",
                        options: (entity) => [{ label: "Dynamic", value: "dyn" }],
                        on_select: (entity, option) => {}
                    }
                ],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        var def = registry.Get("dyn_flow")!;
        var step = (ChoiceStep)def.Steps[0];
        var entity = new Entity("player", "Test");
        var options = step.Options(entity);

        options.Should().HaveCount(1);
        options[0].Label.Should().Be("Dynamic");
    }

    [Fact]
    public void Register_flow_with_text_step()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "text_flow",
                trigger: "t",
                steps: [
                    {
                        id: "input",
                        type: "text",
                        prompt: "Enter value:",
                        on_input: (entity, value) => {}
                    }
                ],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        var def = registry.Get("text_flow")!;
        def.Steps[0].Should().BeOfType<TextStep>();
    }

    [Fact]
    public void Register_flow_with_confirm_step()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "confirm_flow",
                trigger: "t",
                steps: [
                    {
                        id: "q",
                        type: "confirm",
                        prompt: "Sure?",
                        on_yes: (entity) => {},
                        on_no: (entity) => {}
                    }
                ],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        var def = registry.Get("confirm_flow")!;
        def.Steps[0].Should().BeOfType<ConfirmStep>();
    }

    [Fact]
    public void ChoiceStep_option_string_description_is_parsed()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "desc_flow",
                trigger: "t",
                steps: [{
                    id: "pick",
                    type: "choice",
                    prompt: "Choose:",
                    options: [{ label: "Human", value: "human", description: "Warriors of the Waste." }],
                    on_select: (entity, option) => {}
                }],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        var def = registry.Get("desc_flow")!;
        var step = (ChoiceStep)def.Steps[0];
        var entity = new Entity("player", "Test");
        step.Options(entity)[0].Description!.Invoke(entity).Should().Be("Warriors of the Waste.");
    }

    [Fact]
    public void ChoiceStep_option_tag_line_is_parsed()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "tagline_flow",
                trigger: "t",
                steps: [{
                    id: "pick",
                    type: "choice",
                    prompt: "Choose:",
                    options: [{ label: "Human", value: "human", tag_line: "Iron warriors" }],
                    on_select: (entity, option) => {}
                }],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        var def = registry.Get("tagline_flow")!;
        var step = (ChoiceStep)def.Steps[0];
        var entity = new Entity("player", "Test");
        step.Options(entity)[0].TagLine.Should().Be("Iron warriors");
    }

    [Fact]
    public void ChoiceStep_option_function_description_is_invoked_with_entity()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "fn_desc_flow",
                trigger: "t",
                steps: [{
                    id: "pick",
                    type: "choice",
                    prompt: "Choose:",
                    options: [{
                        label: "Human",
                        value: "human",
                        description: (entity) => "Hello " + entity.name + "."
                    }],
                    on_select: (entity, option) => {}
                }],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        var def = registry.Get("fn_desc_flow")!;
        var step = (ChoiceStep)def.Steps[0];
        var entity = new Entity("player", "Rand");
        step.Options(entity)[0].Description!.Invoke(entity).Should().Be("Hello Rand.");
    }

    [Fact]
    public void Flow_wizard_steps_are_parsed()
    {
        var (runtime, registry) = CreateRuntime();

        runtime.Execute("""
            tapestry.flows.register({
                id: "wizard_flow",
                trigger: "t",
                wizard_steps: [
                    { id: "race", label: "Race" },
                    { id: "class", label: "Class" }
                ],
                steps: [
                    {
                        id: "race",
                        type: "choice",
                        prompt: "Choose race:",
                        options: [{ label: "Human", value: "human" }],
                        on_select: (entity, option) => {}
                    }
                ],
                on_complete: (entity) => ({ success: true })
            });
            """, "test");

        var def = registry.Get("wizard_flow")!;
        def.WizardSteps.Should().NotBeNull();
        def.WizardSteps!.Should().HaveCount(2);
        def.WizardSteps![0].StepId.Should().Be("race");
        def.WizardSteps![0].Label.Should().Be("Race");
        def.WizardSteps![1].StepId.Should().Be("class");
    }
}
