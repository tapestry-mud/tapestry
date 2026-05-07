using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Heartbeat;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Sustenance;
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
    private readonly GmcpService _gmcpService;
    private readonly PlayerPersistenceService _persistence;
    private readonly SustenanceConfig _sustenanceConfig;
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
        GmcpService gmcpService,
        PlayerPersistenceService persistence,
        SustenanceConfig sustenanceConfig,
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
        _gmcpService = gmcpService;
        _persistence = persistence;
        _sustenanceConfig = sustenanceConfig;
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
        RegisterSustenanceDrain();
        RegisterAutosave();

        _gameLoop.RegisterRegenHandler(_world, _eventBus,
            regenIntervalTicks: 30,
            sustenanceConfig: _sustenanceConfig,
            restConfig: _restConfig);

        _gameLoop.RegisterTickHandler("gmcp-vitals-flush", 1, () => _gmcpService.FlushDirtyVitals());
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

    private void RegisterSustenanceDrain()
    {
        var lastReminderByEntity = new Dictionary<Guid, long>();

        _gameLoop.RegisterTickHandler("sustenance-drain", _sustenanceConfig.DrainCadence, () =>
        {
            foreach (var entity in _world.GetEntitiesByTag("player"))
            {
                var current = entity.HasProperty(SustenanceProperties.Sustenance)
                    ? entity.GetProperty<int>(SustenanceProperties.Sustenance)
                    : 100;
                var prevTier = _sustenanceConfig.GetTier(current);

                var drainAmount = _sustenanceConfig.DrainAmount;
                var tickEvent = new GameEvent
                {
                    Type = "sustenance.tick",
                    SourceEntityId = entity.Id,
                    Data = new Dictionary<string, object?>
                    {
                        ["entityId"] = entity.Id.ToString(),
                        ["drainAmount"] = drainAmount
                    }
                };
                _eventBus.Publish(tickEvent);
                if (tickEvent.Cancelled) { continue; }
                if (tickEvent.Data.TryGetValue("drainAmount", out var modifiedDrain)
                    && modifiedDrain is int md) { drainAmount = md; }

                var newValue = Math.Max(0, current - drainAmount);
                entity.SetProperty(SustenanceProperties.Sustenance, newValue);

                var newTier = _sustenanceConfig.GetTier(newValue);
                if (newTier != prevTier)
                {
                    _eventBus.Publish(new GameEvent
                    {
                        Type = "sustenance.changed",
                        SourceEntityId = entity.Id,
                        Data = new Dictionary<string, object?>
                        {
                            ["entityId"] = entity.Id.ToString(),
                            ["oldTier"] = prevTier,
                            ["newTier"] = newTier
                        }
                    });
                }

                if (newTier != "full" && newTier == prevTier)
                {
                    var lastReminder = lastReminderByEntity.GetValueOrDefault(entity.Id, 0L);
                    if (_gameLoop.TickCount - lastReminder >= _sustenanceConfig.ReminderIntervalTicks)
                    {
                        lastReminderByEntity[entity.Id] = _gameLoop.TickCount;
                        _eventBus.Publish(new GameEvent
                        {
                            Type = "sustenance.reminder",
                            SourceEntityId = entity.Id,
                            Data = new Dictionary<string, object?>
                            {
                                ["entityId"] = entity.Id.ToString(),
                                ["tier"] = newTier
                            }
                        });
                    }
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
