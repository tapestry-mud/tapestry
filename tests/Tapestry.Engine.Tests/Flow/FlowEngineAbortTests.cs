using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Races;
using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Tests.Flow;

// #20 host side: FlowEngine.Abort routes an unsatisfiable flow. During creation it
// restarts (so the player can re-pick an upstream option, e.g. another race);
// otherwise it drops the flow and returns the player to the world.
public class FlowEngineAbortTests
{
    private class FakePersistence : IFlowPersistence
    {
        public bool PlayerExists(string name) => false;
        public void SaveNewPlayer(Entity entity, Guid accountId) { }
    }

    private static (FlowEngine engine, FlowRegistry registry, SessionManager sessions, PlayerCreator playerCreator)
        Setup()
    {
        var registry = new FlowRegistry();
        var sessions = new SessionManager();
        var playerCreator = new PlayerCreator();
        var world = new World(playerCreator);
        world.AddRoom(new Room("tapestry-core:recall", "Recall", "A safe place."));
        var eventBus = new EventBus();
        var engine = new FlowEngine(registry, sessions, world, new FakePersistence(),
            new PanelRenderer(), new ClassRegistry(), new RaceRegistry(),
            new AlignmentManager(world, eventBus, new AlignmentConfig()), playerCreator, eventBus);
        return (engine, registry, sessions, playerCreator);
    }

    private static FlowDefinition SimpleFlow()
    {
        return new FlowDefinition
        {
            Id = "creation",
            Trigger = "new_player_connect",
            Steps = new[]
            {
                new ChoiceStep
                {
                    Id = "race",
                    Prompt = (_, _) => "Choose your race:",
                    Options = (_, _) => new[] { new ChoiceOption("Human", "human") },
                    OnSelect = (_, _, _) => { }
                }
            },
            OnComplete = (_, _) => new FlowCompletionResult(true)
        };
    }

    private static PlayerSession MakeSession(SessionManager sessions, PlayerCreator playerCreator, LoginPhase phase)
    {
        var entity = new Entity("player", "Tester");
        playerCreator.TrackEntity(entity);
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity) { Phase = phase };
        sessions.Add(session);
        return session;
    }

    [Fact]
    public void Abort_DuringCreation_RestartsFlow_WithFreshInstance()
    {
        var (engine, registry, sessions, playerCreator) = Setup();
        registry.Register(SimpleFlow());
        var session = MakeSession(sessions, playerCreator, LoginPhase.Creating);
        engine.Start(session, "creation");
        var original = session.CurrentFlow;

        engine.Abort(session, "empty_choice");

        session.CurrentFlow.Should().NotBeNull();
        session.CurrentFlow.Should().NotBeSameAs(original);   // restarted, not the same run
        session.Phase.Should().Be(LoginPhase.Creating);
    }

    [Fact]
    public void Abort_OutsideCreation_DropsFlow()
    {
        var (engine, registry, sessions, playerCreator) = Setup();
        registry.Register(SimpleFlow());
        var session = MakeSession(sessions, playerCreator, LoginPhase.Playing);
        engine.Start(session, "creation");

        engine.Abort(session, "empty_choice");

        session.CurrentFlow.Should().BeNull();
    }
}
