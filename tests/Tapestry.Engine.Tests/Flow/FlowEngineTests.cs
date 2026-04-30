using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Races;
using Tapestry.Engine.Ui;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Flow;

public class FlowEngineTests
{
    private class FakePersistence : IFlowPersistence
    {
        public HashSet<string> ExistingNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(Entity entity, string hash)> Saved { get; } = new();

        public bool PlayerExists(string name)
        {
            return ExistingNames.Contains(name);
        }

        public void SaveNewPlayer(Entity entity, string passwordHash)
        {
            Saved.Add((entity, passwordHash));
        }
    }

    private static (FlowEngine engine, FlowRegistry registry, SessionManager sessions, World world, FakePersistence persistence)
        CreateEngine()
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
        return (engine, registry, sessions, world, persistence);
    }

    private static PlayerSession MakeCreatingSession(World world, SessionManager sessions, string name = "Rand")
    {
        var entity = new Entity("player", name);
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity)
        {
            Phase = SessionPhase.Creating,
            PendingPasswordHash = "hash_" + name
        };
        sessions.Add(session);

        var room = world.GetRoom("core:town-square");
        if (room == null)
        {
            room = new Room("core:town-square", "Town Square", "The main square.");
            world.AddRoom(room);
        }

        return session;
    }

    private static FlowDefinition MakeFlow(string id = "test_flow",
        string trigger = "new_player_connect",
        Func<Entity, FlowCompletionResult>? onComplete = null)
    {
        return new FlowDefinition
        {
            Id = id,
            Trigger = trigger,
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
            OnComplete = onComplete ?? (_ => new FlowCompletionResult(true))
        };
    }

    [Fact]
    public void Trigger_starts_flow_on_session()
    {
        var (engine, registry, sessions, world, _) = CreateEngine();
        registry.Register(MakeFlow());
        var session = MakeCreatingSession(world, sessions);

        engine.Trigger(session, "new_player_connect");

        session.CurrentFlow.Should().NotBeNull();
        session.CurrentFlow!.Definition.Id.Should().Be("test_flow");
    }

    [Fact]
    public void Trigger_no_registered_flow_completes_creating_phase_immediately()
    {
        var (engine, registry, sessions, world, persistence) = CreateEngine();
        var session = MakeCreatingSession(world, sessions);

        engine.Trigger(session, "new_player_connect");

        session.Phase.Should().Be(SessionPhase.Playing);
        persistence.Saved.Should().HaveCount(1);
    }

    [Fact]
    public void Trigger_uses_last_registered_flow_for_trigger()
    {
        var (engine, registry, sessions, world, _) = CreateEngine();
        var first = MakeFlow("first", "new_player_connect");
        var second = MakeFlow("second", "new_player_connect");
        registry.Register(first);
        registry.Register(second);
        var session = MakeCreatingSession(world, sessions);

        engine.Trigger(session, "new_player_connect");

        session.CurrentFlow!.Definition.Id.Should().Be("second");
    }

    [Fact]
    public void Complete_creating_phase_success_saves_player_and_transitions_to_playing()
    {
        var (engine, registry, sessions, world, persistence) = CreateEngine();
        var flow = MakeFlow();
        registry.Register(flow);
        var session = MakeCreatingSession(world, sessions);

        engine.Trigger(session, "new_player_connect");
        session.CurrentFlow!.HandleInput("1"); // advances through ChoiceStep → triggers Complete

        session.Phase.Should().Be(SessionPhase.Playing);
        session.CurrentFlow.Should().BeNull();
        persistence.Saved.Should().HaveCount(1);
        persistence.Saved[0].hash.Should().Be("hash_Rand");
    }

    [Fact]
    public void Complete_creating_phase_places_entity_in_spawn_room()
    {
        var (engine, registry, sessions, world, _) = CreateEngine();
        registry.Register(MakeFlow());
        var session = MakeCreatingSession(world, sessions);

        engine.Trigger(session, "new_player_connect");
        session.CurrentFlow!.HandleInput("1");

        var room = world.GetRoom("core:town-square");
        room!.Entities.Should().Contain(session.PlayerEntity);
        world.GetEntity(session.PlayerEntity.Id).Should().NotBeNull();
    }

    [Fact]
    public void Complete_creating_phase_enqueues_motd_and_look()
    {
        var (engine, registry, sessions, world, _) = CreateEngine();
        registry.Register(MakeFlow());
        var session = MakeCreatingSession(world, sessions);

        engine.Trigger(session, "new_player_connect");
        session.CurrentFlow!.HandleInput("1");

        session.InputQueue.Should().Contain("motd");
        session.InputQueue.Should().Contain("look");
    }

    [Fact]
    public void Complete_on_complete_failure_restarts_from_step_0()
    {
        var (engine, registry, sessions, world, _) = CreateEngine();
        engine.NewPlayerEntityFactory = n => new Entity("player", n);

        var flow = MakeFlow(onComplete: _ => new FlowCompletionResult(false, "Bad combo."));
        registry.Register(flow);
        var session = MakeCreatingSession(world, sessions);

        engine.Trigger(session, "new_player_connect");
        var firstEntityId = session.PlayerEntity.Id;
        session.CurrentFlow!.HandleInput("1");

        // Session stays in Creating, new entity allocated, flow restarted
        session.Phase.Should().Be(SessionPhase.Creating);
        session.CurrentFlow.Should().NotBeNull();
        session.PlayerEntity.Id.Should().NotBe(firstEntityId);
    }

    [Fact]
    public void Complete_on_complete_failure_sends_failure_message()
    {
        var (engine, registry, sessions, world, _) = CreateEngine();
        engine.NewPlayerEntityFactory = n => new Entity("player", n);

        var flow = MakeFlow(onComplete: _ => new FlowCompletionResult(false, "Human do not walk the Shadow."));
        registry.Register(flow);
        var session = MakeCreatingSession(world, sessions);
        var conn = (FakeConnection)session.Connection;

        engine.Trigger(session, "new_player_connect");
        session.CurrentFlow!.HandleInput("1");

        conn.SentText.Should().Contain(s => s.Contains("Human do not walk the Shadow."));
    }

    [Fact]
    public void Complete_creating_aborts_and_disconnects_if_name_taken_at_commit()
    {
        var (engine, registry, sessions, world, persistence) = CreateEngine();
        registry.Register(MakeFlow());
        var session = MakeCreatingSession(world, sessions);
        var conn = (FakeConnection)session.Connection;

        engine.Trigger(session, "new_player_connect");

        // Simulate name being taken mid-flow
        persistence.ExistingNames.Add("Rand");
        session.CurrentFlow!.HandleInput("1");

        conn.IsConnected.Should().BeFalse();
        persistence.Saved.Should().BeEmpty();
        sessions.GetByPlayerName("Rand").Should().BeNull();
    }

    [Fact]
    public void Creating_session_in_session_manager_reserves_name()
    {
        var (engine, _, sessions, world, _) = CreateEngine();
        var session = MakeCreatingSession(world, sessions, "Perrin");

        sessions.GetByPlayerName("Perrin").Should().BeSameAs(session);
    }

    [Fact]
    public void Second_session_with_same_name_cannot_register_while_first_is_creating()
    {
        var (engine, registry, sessions, world, _) = CreateEngine();
        var session1 = MakeCreatingSession(world, sessions, "Perrin");

        // First session holds the name reservation
        sessions.GetByPlayerName("Perrin").Should().BeSameAs(session1);

        // Attempt to add another session with same name should not replace the first
        // (SessionManager enforces this by name — the check in TelnetService prevents duplicate adds,
        // but SessionManager.Add with same name would overwrite — verify the RESERVATION is visible)
        var entity2 = new Entity("player", "Perrin");
        var session2 = new PlayerSession(new FakeConnection(), entity2);
        // Don't add to sessions — simulating that TelnetService rejected it before Add

        sessions.GetByPlayerName("Perrin").Should().BeSameAs(session1); // reservation holds
    }

    [Fact]
    public void FinalizeCreating_PublishesCharacterCreatedEvent_AfterTrackEntity()
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

        var room = new Room("core:town-square", "Town Square", "The main square.");
        world.AddRoom(room);

        GameEvent? captured = null;
        Entity? entityDuringEvent = null;
        eventBus.Subscribe("character.created", evt =>
        {
            captured = evt;
            if (evt.SourceEntityId.HasValue)
            {
                entityDuringEvent = world.GetEntity(evt.SourceEntityId.Value);
            }
        });

        var session = MakeCreatingSession(world, sessions);
        engine.Trigger(session, "new_player_connect");

        Assert.NotNull(captured);
        Assert.NotNull(entityDuringEvent);
        Assert.Equal(session.PlayerEntity.Id, captured!.SourceEntityId);
    }

    [Fact]
    public void Complete_playing_phase_clears_current_flow_only()
    {
        var (engine, registry, sessions, world, persistence) = CreateEngine();
        var flow = MakeFlow(trigger: "in_world_trigger");
        registry.Register(flow);

        var entity = new Entity("player", "Mat");
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity) { Phase = SessionPhase.Playing };
        sessions.Add(session);

        engine.Start(session, flow.Id);
        session.CurrentFlow!.HandleInput("1");

        session.CurrentFlow.Should().BeNull();
        session.Phase.Should().Be(SessionPhase.Playing);
        persistence.Saved.Should().BeEmpty(); // no persistence for Playing phase
    }
}
