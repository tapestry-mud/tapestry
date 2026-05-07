using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Sustenance;
using Tapestry.Engine.Training;
using Tapestry.Shared;

namespace Tapestry.Server.Modules;

public class WorldEventModule : IGameModule
{
    private readonly EventBus _eventBus;
    private readonly World _world;
    private readonly MobAIManager _mobAI;
    private readonly PlayerPersistenceService _persistence;
    private readonly SessionManager _sessions;
    private readonly ClassRegistry _classRegistry;
    private readonly TrainingManager _trainingManager;

    public string Name => "WorldEvent";

    public WorldEventModule(
        EventBus eventBus,
        World world,
        MobAIManager mobAI,
        PlayerPersistenceService persistence,
        SessionManager sessions,
        ClassRegistry classRegistry,
        TrainingManager trainingManager)
    {
        _eventBus = eventBus;
        _world = world;
        _mobAI = mobAI;
        _persistence = persistence;
        _sessions = sessions;
        _classRegistry = classRegistry;
        _trainingManager = trainingManager;
    }

    public void Configure()
    {
        WireMovementTracking();
        WirePersistenceSaves();
        WireSustenanceInit();
        RegisterRestAutoWake();
        StatGrowthOnLevelUp.Subscribe(_eventBus, _world, _classRegistry, _trainingManager);
    }

    private void WireMovementTracking()
    {
        _eventBus.Subscribe("player.moved", (evt) =>
        {
            var oldRoomId = evt.Data.TryGetValue("old_room_id", out var oldVal) ? oldVal as string : null;
            var newRoomId = evt.Data.TryGetValue("new_room_id", out var newVal) ? newVal as string : null;

            if (oldRoomId != null)
            {
                _mobAI.PlayerLeftRoom(oldRoomId);
            }
            if (newRoomId != null)
            {
                _mobAI.PlayerEnteredRoom(newRoomId);
            }
        });
    }

    private void WirePersistenceSaves()
    {
        _eventBus.Subscribe("progression.level.up", (evt) =>
        {
            if (evt.SourceEntityId.HasValue)
            {
                var session = _sessions.GetByEntityId(evt.SourceEntityId.Value);
                if (session != null)
                {
                    _ = _persistence.SavePlayer(session);
                }
            }
        });

        _eventBus.Subscribe("entity.vital.depleted", (evt) =>
        {
            if (evt.SourceEntityId.HasValue &&
                evt.Data.TryGetValue("vital", out var vital) &&
                vital?.ToString() == "hp")
            {
                var session = _sessions.GetByEntityId(evt.SourceEntityId.Value);
                if (session != null)
                {
                    _ = _persistence.SavePlayer(session);
                }
            }
        }, priority: 50);
    }

    private void WireSustenanceInit()
    {
        _eventBus.Subscribe("character.created", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            var player = _world.GetEntity(evt.SourceEntityId.Value);
            if (player == null) { return; }
            if (!player.HasProperty(SustenanceProperties.Sustenance))
            {
                player.SetProperty(SustenanceProperties.Sustenance, 100);
            }
        });
    }

    private void RegisterRestAutoWake()
    {
        _eventBus.Subscribe("combat.engage", evt =>
        {
            if (!evt.TargetEntityId.HasValue) { return; }
            var target = _world.GetEntity(evt.TargetEntityId.Value);
            if (target == null) { return; }
            var restState = target.GetProperty<string?>(RestProperties.RestState) ?? RestProperties.StateAwake;
            if (restState == RestProperties.StateResting || restState == RestProperties.StateSleeping)
            {
                target.SetProperty(RestProperties.RestState, RestProperties.StateAwake);
                target.SetProperty(RestProperties.RestTarget, null);
                _eventBus.Publish(new GameEvent
                {
                    Type = "entity.rest_state.changed",
                    SourceEntityId = target.Id,
                    Data = new Dictionary<string, object?>
                    {
                        ["entityId"] = target.Id.ToString(),
                        ["oldState"] = restState,
                        ["newState"] = RestProperties.StateAwake,
                        ["reason"] = "combat"
                    }
                });
            }
        });
    }
}
