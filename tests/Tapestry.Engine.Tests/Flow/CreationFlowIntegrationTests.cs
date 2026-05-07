using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Races;
using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Tests.Flow;

public class CreationFlowIntegrationTests
{
    private class FakePersistence : IFlowPersistence
    {
        public bool PlayerExists(string name) => false;
        public void SaveNewPlayer(Entity entity, string passwordHash) { }
    }

    private static (FlowEngine engine, FlowRegistry registry, SessionManager sessions, World world)
        CreateWiredEngine()
    {
        var registry = new FlowRegistry();
        var sessions = new SessionManager();
        var playerCreator = new PlayerCreator();
        var world = new World(playerCreator);
        var room = new Room("core:town-square", "Town Square", "A busy square.");
        world.AddRoom(room);
        var persistence = new FakePersistence();
        var eventBus = new EventBus();
        var engine = new FlowEngine(registry, sessions, world, persistence, new PanelRenderer(),
            new ClassRegistry(), new RaceRegistry(), new AlignmentManager(world, eventBus, new AlignmentConfig()),
            playerCreator, eventBus)
        {
            NewPlayerEntityFactory = n =>
            {
                var e = new Entity("player", n);
                e.AddTag("player");
                return e;
            }
        };
        return (engine, registry, sessions, world);
    }

    private static FlowDefinition BuildLfCreationFlow()
    {
        return new FlowDefinition
        {
            Id = "lf_character_creation",
            Trigger = "new_player_connect",
            Steps = new FlowStepDefinition[]
            {
                new InfoStep
                {
                    Id = "welcome",
                    Text = _ => "The Wheel of Time turns."
                },
                new ChoiceStep
                {
                    Id = "race",
                    Prompt = _ => "Choose your race:",
                    Options = _ => new[]
                    {
                        new ChoiceOption("Human", "human"),
                        new ChoiceOption("Human", "human")
                    },
                    OnSelect = (entity, opt) => entity.SetProperty("race", opt.Value)
                },
                new ChoiceStep
                {
                    Id = "class",
                    Prompt = _ => "Choose your class:",
                    Options = _ => new[]
                    {
                        new ChoiceOption("Warrior", "warrior"),
                        new ChoiceOption("Mage", "mage")
                    },
                    OnSelect = (entity, opt) => entity.SetProperty("class", opt.Value)
                },
                new ChoiceStep
                {
                    Id = "alignment",
                    Prompt = _ => "Choose your alignment:",
                    Options = _ => new[]
                    {
                        new ChoiceOption("Light", (object)100),
                        new ChoiceOption("Shadow", (object)(-100))
                    },
                    OnSelect = (entity, opt) => entity.SetProperty("alignment", opt.Value)
                }
            },
            OnComplete = entity =>
            {
                var cls = entity.GetProperty<string>("class");
                var race = entity.GetProperty<string>("race");
                if (cls == "mage" && race == "human")
                {
                    return new FlowCompletionResult(false, "Human do not walk the Shadow.");
                }
                return new FlowCompletionResult(true);
            }
        };
    }

    [Fact]
    public void Full_lf_creation_flow_human_warrior_completes_and_spawns()
    {
        var (engine, registry, sessions, world) = CreateWiredEngine();
        registry.Register(BuildLfCreationFlow());

        var entity = new Entity("player", "Rand");
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity)
        {
            Phase = LoginPhase.Creating,
            PendingPasswordHash = "hash"
        };
        sessions.Add(session);

        engine.Trigger(session, "new_player_connect");

        // InfoStep auto-advances — no input needed for welcome
        // Race: choose Human (1)
        session.HandleInput("1");
        // Class: choose Warrior (1)
        session.HandleInput("1");
        // Alignment: choose Light (1)
        session.HandleInput("1");

        session.Phase.Should().Be(LoginPhase.Playing);
        session.CurrentFlow.Should().BeNull();
        session.PlayerEntity.GetProperty<string>("race").Should().Be("human");
        session.PlayerEntity.GetProperty<string>("class").Should().Be("warrior");

        var room = world.GetRoom("core:town-square");
        room!.Entities.Should().Contain(session.PlayerEntity);
    }

    [Fact]
    public void Human_mage_cross_validation_triggers_restart_and_retains_name()
    {
        var (engine, registry, sessions, world) = CreateWiredEngine();
        registry.Register(BuildLfCreationFlow());

        var entity = new Entity("player", "Ishamael");
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity)
        {
            Phase = LoginPhase.Creating,
            PendingPasswordHash = "hash"
        };
        sessions.Add(session);

        engine.Trigger(session, "new_player_connect");

        session.HandleInput("2"); // Human
        session.HandleInput("2"); // Mage
        session.HandleInput("2"); // Shadow — triggers on_complete failure

        session.Phase.Should().Be(LoginPhase.Creating); // still creating
        session.PlayerEntity.Name.Should().Be("Ishamael"); // name preserved
        session.CurrentFlow.Should().NotBeNull(); // restarted flow
    }

    [Fact]
    public void Session_remains_in_session_manager_during_creation()
    {
        var (engine, registry, sessions, world) = CreateWiredEngine();
        registry.Register(BuildLfCreationFlow());

        var entity = new Entity("player", "Egwene");
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity)
        {
            Phase = LoginPhase.Creating,
            PendingPasswordHash = "hash"
        };
        sessions.Add(session);

        engine.Trigger(session, "new_player_connect");

        // Mid-flow — name should be reserved
        sessions.GetByPlayerName("Egwene").Should().BeSameAs(session);
        // Entity is findable via PlayerCreator fallthrough, but not yet promoted to live world tracking
        world.GetEntity(entity.Id).Should().BeSameAs(entity); // findable via PlayerCreator
        world.GetAllTrackedEntities().Should().NotContain(entity); // not yet promoted
    }
}
