using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Races;
using Tapestry.Engine.Ui;
using Tapestry.Shared;

namespace Tapestry.Engine.Flow;

public class FlowEngine
{
    private readonly FlowRegistry _registry;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly IFlowPersistence _persistence;
    private readonly PanelRenderer _panelRenderer;
    private readonly ClassRegistry _classRegistry;
    private readonly RaceRegistry _raceRegistry;
    private readonly AlignmentManager _alignmentManager;
    private readonly PlayerCreator _playerCreator;
    private readonly EventBus _eventBus;
    private readonly object _commitLock = new();

    public string DefaultSpawnRoomId { get; set; } = "core:town-square";

    /// Factory used by Restart to rebuild a fresh entity with the same name.
    /// TelnetService sets this on startup so stat defaults match initial creation.
    public Func<string, Entity>? NewPlayerEntityFactory { get; set; }

    public Action<string, string, object>? GmcpSend { get; set; }

    public FlowEngine(
        FlowRegistry registry,
        SessionManager sessions,
        World world,
        IFlowPersistence persistence,
        PanelRenderer panelRenderer,
        ClassRegistry classRegistry,
        RaceRegistry raceRegistry,
        AlignmentManager alignmentManager,
        PlayerCreator playerCreator,
        EventBus eventBus)
    {
        _registry = registry;
        _sessions = sessions;
        _world = world;
        _persistence = persistence;
        _panelRenderer = panelRenderer;
        _classRegistry = classRegistry;
        _raceRegistry = raceRegistry;
        _alignmentManager = alignmentManager;
        _playerCreator = playerCreator;
        _eventBus = eventBus;
    }

    public void Start(PlayerSession session, string flowId)
    {
        var definition = _registry.Get(flowId);
        if (definition == null) { return; }

        var instance = new FlowInstance(definition, session.PlayerEntity, _panelRenderer);
        instance.OnCompleted = () => Complete(session);
        instance.GmcpSend = GmcpSend;
        instance.CommandFallback = input => session.EnqueueInput(input);
        session.CurrentFlow = instance;
        _playerCreator.TrackEntity(session.PlayerEntity);
        instance.Start(session);
    }

    public void Trigger(PlayerSession session, string triggerName)
    {
        var flows = _registry.GetByTrigger(triggerName);
        var flow = flows.LastOrDefault();

        if (flow == null)
        {
            if (session.Phase == LoginPhase.Creating)
            {
                FinalizeCreating(session);
            }
            return;
        }

        Start(session, flow.Id);
    }

    public void Complete(PlayerSession session)
    {
        var instance = session.CurrentFlow;
        if (instance == null) { return; }

        if (session.Phase == LoginPhase.Creating)
        {
            SeedCharacterAlignment(instance.Entity);
        }

        var result = instance.Definition.OnComplete(instance.Entity);

        if (!result.Success)
        {
            if (result.Message != null) { session.SendLine(result.Message); }
            if (session.Phase == LoginPhase.Creating)
            {
                Restart(session, "validation_failed");
            }
            else
            {
                session.CurrentFlow = null;
                session.EnqueueInput("look");
            }
            return;
        }

        if (session.Phase == LoginPhase.Creating)
        {
            FinalizeCreating(session);
        }
        else
        {
            session.CurrentFlow = null;
            session.EnqueueInput("look");
        }
    }

    public void Restart(PlayerSession session, string reason)
    {
        var oldEntityId = session.PlayerEntity.Id;
        var flowId = session.CurrentFlow!.Definition.Id;
        var factory = NewPlayerEntityFactory ?? (n => new Entity("player", n));
        var newEntity = factory(session.PlayerEntity.Name);

        session.ReplaceEntity(newEntity);
        _sessions.UpdateEntityId(oldEntityId, session);
        session.CurrentFlow = null;

        _playerCreator.Remove(oldEntityId);
        Start(session, flowId);
    }

    private void SeedCharacterAlignment(Entity entity)
    {
        var classId = entity.GetProperty<string>("class");
        var raceId = entity.GetProperty<string>("race");
        if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(raceId)) { return; }

        var classDef = _classRegistry.Get(classId);
        var raceDef = _raceRegistry.Get(raceId);
        if (classDef == null || raceDef == null) { return; }

        var total = classDef.StartingAlignment + raceDef.StartingAlignment;
        _alignmentManager.Set(entity.Id, total, "character_creation");
    }

    private void FinalizeCreating(PlayerSession session)
    {
        var entity = session.PlayerEntity;

        lock (_commitLock)
        {
            if (_persistence.PlayerExists(entity.Name))
            {
                session.SendLine(
                    "That name was taken while you were creating your character. " +
                    "Please reconnect and try a different name.");
                session.CurrentFlow = null;
                _sessions.Remove(session);
                session.Connection.Disconnect("name conflict at commit");
                return;
            }

            _persistence.SaveNewPlayer(entity, session.PendingPasswordHash!);
        }

        var spawnRoom = _world.GetRoom(entity.LocationRoomId ?? DefaultSpawnRoomId)
                        ?? _world.GetRoom(DefaultSpawnRoomId)
                        ?? _world.AllRooms.FirstOrDefault();

        if (spawnRoom != null) { spawnRoom.AddEntity(entity); }

        _playerCreator.Remove(entity.Id);
        _world.TrackEntity(entity);

        _eventBus.Publish(new GameEvent
        {
            Type = "character.created",
            SourceEntityId = entity.Id,
            Data = new Dictionary<string, object?>()
        });

        session.CancelPreLoginTimeout?.Invoke();
        session.CancelPreLoginTimeout = null;
        session.Phase = LoginPhase.Playing;
        session.CurrentFlow = null;

        session.SendLine("");
        session.SendLine($"Welcome to the world, {entity.Name}!");
        session.SendLine("");

        session.EnqueueInput("motd");
        session.EnqueueInput("look");
    }
}
