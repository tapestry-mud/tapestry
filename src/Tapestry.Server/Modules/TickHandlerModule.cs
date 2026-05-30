using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Heartbeat;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Rest;
using Tapestry.Shared;

namespace Tapestry.Server.Modules;

public class TickHandlerModule : IGameModule
{
    private readonly GameLoop _gameLoop;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly HeartbeatManager _heartbeat;
    private readonly CombatPulse _combatPulse;
    private readonly MobAIManager _mobAI;
    private readonly MobCommandQueue _mobCommandQueue;
    private readonly DirtyVitalsBatcher _dirtyVitalsBatcher;
    private readonly PlayerPersistenceService _persistence;
    private readonly RestConfig _restConfig;
    private readonly ServerConfig _config;
    private readonly AreaTickService _areaTick;
    private readonly GameClock _gameClock;
    private readonly TickTimer _tickTimer;
    private readonly SessionManager _sessions;

    public string Name => "TickHandler";

    public TickHandlerModule(
        GameLoop gameLoop,
        World world,
        EventBus eventBus,
        HeartbeatManager heartbeat,
        CombatPulse combatPulse,
        MobAIManager mobAI,
        MobCommandQueue mobCommandQueue,
        DirtyVitalsBatcher dirtyVitalsBatcher,
        PlayerPersistenceService persistence,
        RestConfig restConfig,
        ServerConfig config,
        AreaTickService areaTick,
        GameClock gameClock,
        TickTimer tickTimer,
        SessionManager sessions)
    {
        _gameLoop = gameLoop;
        _world = world;
        _eventBus = eventBus;
        _heartbeat = heartbeat;
        _combatPulse = combatPulse;
        _mobAI = mobAI;
        _mobCommandQueue = mobCommandQueue;
        _dirtyVitalsBatcher = dirtyVitalsBatcher;
        _persistence = persistence;
        _restConfig = restConfig;
        _config = config;
        _areaTick = areaTick;
        _gameClock = gameClock;
        _tickTimer = tickTimer;
        _sessions = sessions;
    }

    public void Configure()
    {
        _gameLoop.SetPreTickAction(() =>
        {
            _world.SwapTagBuffers();
            System.Diagnostics.Activity.Current?.SetTag("swap.dirty_tags", _world.LastSwapDirtyCount);
            System.Diagnostics.Activity.Current?.SetTag("swap.tag_count", _world.LastSwapTagCount);
        });

        _gameLoop.RegisterTickHandler("area-tick", 1, () => _areaTick.Tick());
        _gameLoop.RegisterTickHandler("game-clock", 1, () => _gameClock.Tick());
        _gameLoop.RegisterTickHandler("tick-timer", 1, () => _tickTimer.Advance());

        _gameLoop.RegisterTickHandler("mob-ai", 10, () => _mobAI.Tick());
        _gameLoop.RegisterTickHandler("mob-command-queue", 1, () => _mobCommandQueue.ProcessTick());

        _heartbeat.Register(_combatPulse);
        _gameLoop.RegisterTickHandler("heartbeat", 1, () => _heartbeat.Tick());

        RegisterCorpseDecay();
        RegisterAutosave();

        _gameLoop.RegisterRegenHandler(_world, _eventBus,
            regenIntervalTicks: 30,
            restConfig: _restConfig);

        _gameLoop.RegisterTickHandler("gmcp-vitals-flush", 1, () => _dirtyVitalsBatcher.FlushDirtyVitals());

        // Resume any flow that is awaiting an async result (e.g. a recommend side-action).
        // TryResumeAsync is a no-op when nothing is pending, so sweeping every session each
        // tick is cheap; the continuation runs here on the game thread.
        _gameLoop.RegisterTickHandler("flow-async-resume", 1, () =>
        {
            foreach (var session in _sessions.AllSessions)
            {
                session.CurrentFlow?.TryResumeAsync();
            }
        });
    }

    private void RegisterCorpseDecay()
    {
        _gameLoop.RegisterTickHandler("corpse-decay", 30, () =>
        {
            foreach (var corpse in _world.GetEntitiesByTag("corpse"))
            {
                var decayTicks = corpse.GetProperty<int>(CommonProperties.CorpseDecay);
                var createdTick = corpse.GetProperty<long>(CommonProperties.CorpseCreatedTick);
                if (decayTicks > 0 && _gameLoop.TickCount - createdTick >= decayTicks)
                {
                    var roomId = corpse.LocationRoomId;
                    Room? room = null;
                    if (roomId != null)
                    {
                        room = _world.GetRoom(roomId);
                    }

                    var dumpedItemIds = corpse.Contents.Select(i => i.Id.ToString()).ToList();

                    if (room != null)
                    {
                        foreach (var item in corpse.Contents.ToList())
                        {
                            corpse.RemoveFromContents(item);
                            room.AddEntity(item);
                        }
                    }

                    _eventBus.Publish(new GameEvent
                    {
                        Type = "corpse.decayed",
                        SourceEntityId = corpse.Id,
                        RoomId = roomId,
                        Data =
                        {
                            ["corpseName"] = corpse.Name,
                            ["wasPlayerCorpse"] = corpse.HasTag("player_corpse"),
                            ["roomId"] = roomId,
                            ["itemIds"] = dumpedItemIds
                        }
                    });

                    if (room != null)
                    {
                        room.RemoveEntity(corpse);
                        _sessions.SendToRoom(roomId!, corpse.Name + " crumbles to dust.\r\n");
                    }
                    _world.UntrackEntity(corpse);
                }
            }
        });
    }

    private void RegisterAutosave()
    {
        _gameLoop.RegisterTickHandler("autosave", _config.Persistence.AutosaveInterval, () =>
        {
            var snapshots = _persistence.SnapshotAllPlayers();
            if (snapshots.Count > 0)
            {
                _ = Task.Run(() => _persistence.WriteSnapshotsAsync(snapshots));
            }
        });
    }
}
