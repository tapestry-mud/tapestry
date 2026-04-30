using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Races;
using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Tests.Flow;

public class FlowAlignmentSeedingTests
{
    private class FakePersistence : IFlowPersistence
    {
        public bool PlayerExists(string name) => false;
        public void SaveNewPlayer(Entity entity, string passwordHash) { }
    }

    private (FlowEngine engine, SessionManager sessions, World world,
             ClassRegistry classRegistry, RaceRegistry raceRegistry, AlignmentManager alignmentManager,
             PlayerCreator playerCreator)
        Setup()
    {
        var flowRegistry = new FlowRegistry();
        var sessions = new SessionManager();
        var playerCreator = new PlayerCreator();
        var world = new World(playerCreator);
        var room = new Room("core:town-square", "Town Square", "Square.");
        world.AddRoom(room);
        var eventBus = new EventBus();
        var classRegistry = new ClassRegistry();
        var raceRegistry = new RaceRegistry();
        var alignmentManager = new AlignmentManager(world, eventBus, new AlignmentConfig());
        var engine = new FlowEngine(flowRegistry, sessions, world, new FakePersistence(),
            new PanelRenderer(), classRegistry, raceRegistry, alignmentManager, playerCreator, eventBus);
        return (engine, sessions, world, classRegistry, raceRegistry, alignmentManager, playerCreator);
    }

    private PlayerSession MakeCreatingSession(World world, SessionManager sessions, Entity entity, PlayerCreator playerCreator)
    {
        playerCreator.TrackEntity(entity);
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity)
        {
            Phase = SessionPhase.Creating,
            PendingPasswordHash = "hash"
        };
        sessions.Add(session);
        return session;
    }

    private static void SetCurrentFlow(PlayerSession session, Entity entity,
        Func<Entity, FlowCompletionResult>? onComplete = null)
    {
        var def = new FlowDefinition
        {
            Id = "test_flow",
            Trigger = "new_player_connect",
            Steps = Array.Empty<FlowStepDefinition>(),
            OnComplete = onComplete ?? (_ => new FlowCompletionResult(true))
        };
        session.CurrentFlow = new FlowInstance(def, entity);
    }

    [Fact]
    public void Complete_SeedsAlignment_SumsClassAndRace()
    {
        var (engine, sessions, world, classRegistry, raceRegistry, alignmentManager, playerCreator) = Setup();
        classRegistry.Register(new ClassDefinition { Id = "mage", Name = "Mage", StartingAlignment = -400 });
        raceRegistry.Register(new RaceDefinition { Id = "elf", Name = "Elf", StartingAlignment = -100 });

        var entity = new Entity("player", "Ishamael");
        entity.SetProperty("class", "mage");
        entity.SetProperty("race", "elf");

        var session = MakeCreatingSession(world, sessions, entity, playerCreator);
        SetCurrentFlow(session, entity);
        engine.Complete(session);

        Assert.Equal(-500, alignmentManager.Get(entity.Id));
    }

    [Fact]
    public void Complete_ClampsAlignment_WhenSumExceedsEngineBounds()
    {
        var (engine, sessions, world, classRegistry, raceRegistry, alignmentManager, playerCreator) = Setup();
        classRegistry.Register(new ClassDefinition { Id = "mage", Name = "Mage", StartingAlignment = -900 });
        raceRegistry.Register(new RaceDefinition { Id = "elf", Name = "Elf", StartingAlignment = -900 });

        var entity = new Entity("player", "Graendal");
        entity.SetProperty("class", "mage");
        entity.SetProperty("race", "elf");

        var session = MakeCreatingSession(world, sessions, entity, playerCreator);
        SetCurrentFlow(session, entity);
        engine.Complete(session);

        Assert.Equal(-1000, alignmentManager.Get(entity.Id));
    }

    [Fact]
    public void Complete_SkipsAlignment_WhenEntityHasNoClass()
    {
        var (engine, sessions, world, classRegistry, raceRegistry, alignmentManager, playerCreator) = Setup();
        raceRegistry.Register(new RaceDefinition { Id = "human", Name = "Human", StartingAlignment = 50 });

        var entity = new Entity("player", "NoClass");
        entity.SetProperty("race", "human");

        var session = MakeCreatingSession(world, sessions, entity, playerCreator);
        SetCurrentFlow(session, entity);
        engine.Complete(session);

        Assert.Equal(0, alignmentManager.Get(entity.Id));
    }

    [Fact]
    public void Complete_SeedsAlignment_BeforeOnCompleteCallback()
    {
        var (engine, sessions, world, classRegistry, raceRegistry, alignmentManager, playerCreator) = Setup();
        classRegistry.Register(new ClassDefinition { Id = "warrior", Name = "Warrior", StartingAlignment = 0 });
        raceRegistry.Register(new RaceDefinition { Id = "human", Name = "Human", StartingAlignment = 0 });

        var entity = new Entity("player", "Rand");
        entity.SetProperty("class", "warrior");
        entity.SetProperty("race", "human");

        int? alignmentDuringCallback = null;
        var session = MakeCreatingSession(world, sessions, entity, playerCreator);
        SetCurrentFlow(session, entity, e =>
        {
            alignmentDuringCallback = e.GetProperty<int?>("alignment");
            return new FlowCompletionResult(true);
        });
        engine.Complete(session);

        Assert.Equal(0, alignmentDuringCallback);
    }
}
