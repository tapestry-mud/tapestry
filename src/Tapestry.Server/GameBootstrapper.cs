using Microsoft.Extensions.Logging;
using Tapestry.Data;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Color;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Races;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Training;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Sustenance;
using Tapestry.Engine.Consumables;
using Tapestry.Engine.Containers;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Heartbeat;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Prompt;
using Tapestry.Scripting;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Modules;
using Tapestry.Scripting.Services;
using Tapestry.Shared;

namespace Tapestry.Server;

public class GameBootstrapper
{
    private readonly GameLoop _gameLoop;
    private readonly EventBus _eventBus;
    private readonly CombatManager _combat;
    private readonly SpawnManager _spawns;
    private readonly MobAIManager _mobAI;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly PackLoader _packLoader;
    private readonly ConnectionLoader _connectionLoader;
    private readonly ThemeRegistry _themeRegistry;
    private readonly ApiMessaging _messaging;
    private readonly PropertyTypeRegistry _propertyRegistry;
    private readonly PlayerPersistenceService _persistence;
    private readonly CommandRegistry _commandRegistry;
    private readonly EffectManager _effectManager;
    private readonly AbilityCommandBridge _abilityCommandBridge;
    private readonly AbilityRegistry _abilityRegistry;
    private readonly ProficiencyManager _proficiencyManager;
    private readonly HeartbeatManager _heartbeat;
    private readonly CombatPulse _combatPulse;
    private readonly AbilityResolutionPhase _abilityResolutionPhase;
    private readonly ClassRegistry _classRegistry;
    private readonly RaceRegistry _raceRegistry;
    private readonly ClassPathProcessor _classPathProcessor;
    private readonly PlayerCreator _playerCreator;
    private readonly Tapestry.Scripting.Modules.CommandsModule _commandsModule;
    private readonly ServerConfig _config;
    private readonly TrainingManager _trainingManager;
    private readonly TrainingConfig _trainingConfig;
    private readonly EconomyConfig _economyConfig;
    private readonly SustenanceConfig _sustenanceConfig;
    private readonly RestConfig _restConfig;
    private readonly TemporaryExitService _temporaryExits;
    private readonly GameClock _gameClock;
    private readonly WeatherService _weatherService;
    private readonly AreaTickService _areaTick;
    private readonly GmcpService _gmcpService;
    private readonly TickTimer _tickTimer;
    private readonly ILogger<GameBootstrapper> _logger;

    public GameBootstrapper(
        GameLoop gameLoop,
        EventBus eventBus,
        CombatManager combat,
        SpawnManager spawns,
        MobAIManager mobAI,
        SessionManager sessions,
        World world,
        PackLoader packLoader,
        ConnectionLoader connectionLoader,
        ThemeRegistry themeRegistry,
        ApiMessaging messaging,
        PropertyTypeRegistry propertyRegistry,
        PlayerPersistenceService persistence,
        CommandRegistry commandRegistry,
        EffectManager effectManager,
        AbilityCommandBridge abilityCommandBridge,
        AbilityRegistry abilityRegistry,
        ProficiencyManager proficiencyManager,
        HeartbeatManager heartbeat,
        CombatPulse combatPulse,
        AbilityResolutionPhase abilityResolutionPhase,
        ClassRegistry classRegistry,
        RaceRegistry raceRegistry,
        ClassPathProcessor classPathProcessor,
        PlayerCreator playerCreator,
        Tapestry.Scripting.Modules.CommandsModule commandsModule,
        ServerConfig config,
        TrainingManager trainingManager,
        TrainingConfig trainingConfig,
        EconomyConfig economyConfig,
        SustenanceConfig sustenanceConfig,
        RestConfig restConfig,
        TemporaryExitService temporaryExits,
        GameClock gameClock,
        WeatherService weatherService,
        AreaTickService areaTick,
        GmcpService gmcpService,
        TickTimer tickTimer,
        ILogger<GameBootstrapper> logger)
    {
        _gameLoop = gameLoop;
        _eventBus = eventBus;
        _combat = combat;
        _spawns = spawns;
        _mobAI = mobAI;
        _sessions = sessions;
        _world = world;
        _packLoader = packLoader;
        _connectionLoader = connectionLoader;
        _themeRegistry = themeRegistry;
        _messaging = messaging;
        _propertyRegistry = propertyRegistry;
        _persistence = persistence;
        _commandRegistry = commandRegistry;
        _effectManager = effectManager;
        _abilityCommandBridge = abilityCommandBridge;
        _abilityRegistry = abilityRegistry;
        _proficiencyManager = proficiencyManager;
        _heartbeat = heartbeat;
        _combatPulse = combatPulse;
        _abilityResolutionPhase = abilityResolutionPhase;
        _classRegistry = classRegistry;
        _raceRegistry = raceRegistry;
        _classPathProcessor = classPathProcessor;
        _playerCreator = playerCreator;
        _commandsModule = commandsModule;
        _config = config;
        _trainingManager = trainingManager;
        _trainingConfig = trainingConfig;
        _economyConfig = economyConfig;
        _sustenanceConfig = sustenanceConfig;
        _restConfig = restConfig;
        _temporaryExits = temporaryExits;
        _gameClock = gameClock;
        _weatherService = weatherService;
        _areaTick = areaTick;
        _gmcpService = gmcpService;
        _tickTimer = tickTimer;
        _logger = logger;
    }

    public void Configure()
    {
        ConfigureTraining();
        ConfigureEconomy();
        RegisterPropertyTypes();
        _messaging.SetMotd(_config.Server.Motd);
        LoadPacks();
        _connectionLoader.Load();
        AppendPackCreditsToMotd();
        _abilityCommandBridge.WireAll();
        _commandsModule.LogLoadTimeWarnings();
        LoadSeedPlayers();
        CompileThemes();
        WireEventHandlers();
        RegisterTickHandlers();
        RegisterPersistenceCommands();
        RunInitialSpawns();
    }

    private void ConfigureTraining()
    {
        _trainingConfig.Configure(
            _config.Training.RequireSafeRoomForStats,
            _config.Training.TrainableStats,
            _config.Training.CatchUpBoost);
    }

    private void ConfigureEconomy()
    {
        _economyConfig.Configure(
            _config.Economy.ShopBuyMarkup,
            _config.Economy.ShopSellDiscount);
    }

    private void RegisterPropertyTypes()
    {
        CommonProperties.Register(_propertyRegistry);
        CombatProperties.Register(_propertyRegistry);
        InventoryProperties.Register(_propertyRegistry);
        ItemProperties.Register(_propertyRegistry);
        MobProperties.Register(_propertyRegistry);
        ProgressionProperties.Register(_propertyRegistry);
        PromptProperties.Register(_propertyRegistry);
        AbilityProperties.Register(_propertyRegistry);
        TrainingProperties.Register(_propertyRegistry);
        CurrencyProperties.Register(_propertyRegistry);
        SustenanceProperties.Register(_propertyRegistry);
        ConsumableProperties.Register(_propertyRegistry);
        ContainerProperties.Register(_propertyRegistry);
        RestProperties.Register(_propertyRegistry);
    }

    private void LoadPacks()
    {
        var packsDir = Path.Combine(AppContext.BaseDirectory, "packs");

        foreach (var packName in _config.Packs)
        {
            var packDir = Path.Combine(packsDir, packName);
            if (Directory.Exists(packDir))
            {
                _packLoader.Load(packDir);
                _logger.LogInformation("Loaded pack: {Pack}", packName);
            }
            else
            {
                _logger.LogWarning("Pack not found: {Pack} (looked in {Dir})", packName, packDir);
            }
        }

        _packLoader.ValidateAreaWeatherZones();
    }

    private void AppendPackCreditsToMotd()
    {
        var packs = _packLoader.LoadedPacks;
        if (packs.Count == 0) { return; }

        var credits = string.Join(", ", packs.Select(p =>
        {
            var label = string.IsNullOrEmpty(p.DisplayName) ? p.Name : p.DisplayName;
            return string.IsNullOrEmpty(p.Author) ? $"{label} v{p.Version}" : $"{label} v{p.Version} by {p.Author}";
        }));

        var current = _messaging.GetMotd();
        _messaging.SetMotd(current + $"\r\n[ Packs: {credits} ]");
    }

    private void CompileThemes()
    {
        _themeRegistry.Compile();
    }

    private void WireEventHandlers()
    {
        WireAggroCombat();
        WireDeathHandling();
        WireMovementTracking();
        WirePersistenceSaves();
        WirePulseDelayClearing();
        WireSustenanceInit();
        RegisterRestAutoWake();
        StatGrowthOnLevelUp.Subscribe(_eventBus, _world, _classRegistry, _trainingManager);
        WireGmcpHandlers();
    }

    private void WireGmcpHandlers()
    {
        _eventBus.Subscribe("player.moved", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _gmcpService.SendRoomInfoForEntity(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("player.move.failed", evt =>
        {
            Guid entityId;
            if (evt.SourceEntityId.HasValue)
            {
                entityId = evt.SourceEntityId.Value;
            }
            else if (evt.Data.TryGetValue("entityId", out var idObj)
                && Guid.TryParse(idObj?.ToString(), out var parsed))
            {
                entityId = parsed;
            }
            else { return; }
            _gmcpService.SendRoomWrongDir(entityId);
        });

        _eventBus.Subscribe("progression.level.up", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _gmcpService.SendCharStatus(evt.SourceEntityId.Value);
            _gmcpService.MarkVitalsDirty(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.regen", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _gmcpService.MarkVitalsDirty(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.vital.depleted", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            _gmcpService.MarkVitalsDirty(evt.SourceEntityId.Value);
        }, priority: -10);

        _eventBus.Subscribe("character.created", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            var session = _sessions.GetByEntityId(evt.SourceEntityId.Value);
            if (session == null) { return; }
            var entity = _world.GetEntity(evt.SourceEntityId.Value);
            if (entity == null) { return; }
            _gmcpService.OnPlayerLoggedIn(session.Connection.Id, entity);
        });
    }

    private void WirePulseDelayClearing()
    {
        _eventBus.Subscribe("combat.flee", evt =>
        {
            if (evt.SourceEntityId.HasValue)
            {
                _abilityResolutionPhase.ClearPulseDelays(evt.SourceEntityId.Value);
            }
        });

        _eventBus.Subscribe("combat.end", evt =>
        {
            if (evt.SourceEntityId.HasValue)
            {
                _abilityResolutionPhase.ClearPulseDelays(evt.SourceEntityId.Value);
            }
        });
    }

    private void WireAggroCombat()
    {
        _eventBus.Subscribe("mob.aggro", (evt) =>
        {
            if (evt.Data.TryGetValue("attackerId", out var atkIdObj) &&
                evt.Data.TryGetValue("targetId", out var tgtIdObj))
            {
                var attackerId = Guid.Parse(atkIdObj?.ToString() ?? "");
                var targetId = Guid.Parse(tgtIdObj?.ToString() ?? "");
                var attacker = _world.GetEntity(attackerId);
                var target = _world.GetEntity(targetId);
                if (attacker != null && target != null)
                {
                    _combat.Engage(attacker, target, _gameLoop.TickCount);
                }
            }
        });
    }

    private void WireDeathHandling()
    {
        _eventBus.Subscribe("entity.vital.depleted", (evt) =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("vital", out var vital) || vital?.ToString() != "hp") { return; }

            var victimId = evt.SourceEntityId.Value;

            // Resolve killer: prefer explicit attackerId from event (ability one-shots),
            // fall back to combat primary target (auto-attack kills).
            Guid? killerId = null;
            if (evt.Data.TryGetValue("attackerId", out var attackerIdStr)
                && Guid.TryParse(attackerIdStr?.ToString(), out var parsedAttackerId))
            {
                killerId = parsedAttackerId;
            }
            else
            {
                killerId = _combat.GetPrimaryTarget(victimId);
            }

            // Fire cancellable death check — pack skills (e.g. cockroach) subscribe here
            // and set cancel = true to intercept the death and handle resurrection instead.
            var deathCheck = new GameEvent
            {
                Type = "entity.death.check",
                SourceEntityId = victimId,
                TargetEntityId = killerId,
                RoomId = _world.GetEntity(victimId)?.LocationRoomId,
                Data = new Dictionary<string, object?>
                {
                    ["victimId"] = victimId.ToString(),
                    ["killerId"] = killerId?.ToString(),
                    ["cancel"] = (object)false
                }
            };
            _eventBus.Publish(deathCheck);

            if (deathCheck.Data["cancel"] is true) { return; }

            _combat.HandleEntityDeath(victimId, killerId);
        }, priority: 100);
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

    private void RegisterTickHandlers()
    {
        _gameLoop.RegisterTickHandler("area-tick", 1, () => _areaTick.Tick());
        _gameLoop.RegisterTickHandler("game-clock", 1, () => _gameClock.Tick());
        _gameLoop.RegisterTickHandler("tick-timer", 1, () => _tickTimer.Advance());
        RegisterMobAI();
        RegisterHeartbeat();
        RegisterCorpseDecay();
        RegisterSustenanceDrain();

        RegisterAutosave();

        _gameLoop.RegisterRegenHandler(_world, _eventBus,
            regenIntervalTicks: 30,
            sustenanceConfig: _sustenanceConfig,
            restConfig: _restConfig);

        _gameLoop.RegisterTickHandler("gmcp-vitals-flush", 1, () => _gmcpService.FlushDirtyVitals());
    }

    private void RegisterHeartbeat()
    {
        _heartbeat.World = _world;
        _heartbeat.EventBus = _eventBus;
        _heartbeat.CombatManager = _combat;
        _heartbeat.AbilityRegistry = _abilityRegistry;
        _heartbeat.ProficiencyManager = _proficiencyManager;
        _heartbeat.EffectManager = _effectManager;
        _heartbeat.SessionManager = _sessions;

        _heartbeat.Register(_combatPulse);

        _gameLoop.RegisterTickHandler("heartbeat", 1, () => _heartbeat.Tick());
    }

    private void RegisterMobAI()
    {
        _gameLoop.RegisterTickHandler("mob-ai", 10, () => _mobAI.Tick());
    }

    private void RegisterCorpseDecay()
    {
        _gameLoop.RegisterTickHandler("corpse-decay", 30, () =>
        {
            var corpses = _world.GetEntitiesByTag("corpse").ToList();
            foreach (var corpse in corpses)
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
                    // Save after corpse creation (runs after death handler at priority 100)
                    _ = _persistence.SavePlayer(session);
                }
            }
        }, priority: 50);
    }

    private void RegisterSustenanceDrain()
    {
        var lastReminderByEntity = new Dictionary<Guid, long>();

        _gameLoop.RegisterTickHandler("sustenance-drain", _sustenanceConfig.DrainCadence, () =>
        {
            foreach (var entity in _world.GetEntitiesByTag("player").ToList())
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

    private void RegisterPersistenceCommands()
    {
        _commandRegistry.Register("save", (ctx) =>
        {
            var session = _sessions.GetByEntityId(ctx.PlayerEntityId);
            if (session != null)
            {
                _ = _persistence.SavePlayer(session);
                _sessions.SendToPlayer(ctx.PlayerEntityId, "Character saved.\r\n");
            }
        }, priority: 100, packName: "core",
           description: "Save your character to disk.",
           category: "system");

        _commandRegistry.Register("resetpassword", (ctx) =>
        {
            var session = _sessions.GetByEntityId(ctx.PlayerEntityId);
            if (session == null)
            {
                return;
            }

            if (ctx.Args.Length == 0)
            {
                var room = _world.GetRoom(session.PlayerEntity.LocationRoomId ?? "");
                if (room == null || !room.HasTag("safe"))
                {
                    _sessions.SendToPlayer(ctx.PlayerEntityId,
                        "You must be in a safe area to reset your password.\r\n");
                    return;
                }

                session.InputMode = InputMode.Prompt;
                _gmcpService.SendLoginPhase(session.Connection.Id, "password");
                session.Connection.SuppressEcho();
                _sessions.SendToPlayer(ctx.PlayerEntityId, "Enter current password:\r\n");

                void ExitPrompt(string message)
                {
                    session.Connection.RestoreEcho();
                    _gmcpService.SendLoginPhase(session.Connection.Id, "playing");
                    session.InputMode = InputMode.Normal;
                    session.PromptHandler = null;
                    _sessions.SendToPlayer(ctx.PlayerEntityId, message + "\r\n");
                }

                session.PromptHandler = (currentPw) =>
                {
                    currentPw = currentPw.Trim();
                    var existingHash = _persistence.GetPasswordHash(session.PlayerEntity.Id);
                    if (existingHash == null || !BCrypt.Net.BCrypt.Verify(currentPw, existingHash))
                    {
                        ExitPrompt("Incorrect password. Password reset cancelled.");
                        return;
                    }

                    session.Connection.SendLine("Enter new password:");
                    session.PromptHandler = (newPw) =>
                    {
                        newPw = newPw.Trim();
                        if (newPw.Length < _config.Persistence.PasswordMinLength)
                        {
                            ExitPrompt(
                                $"Password must be at least {_config.Persistence.PasswordMinLength} characters. " +
                                "Password reset cancelled.");
                            return;
                        }

                        session.Connection.SendLine("Confirm new password:");
                        session.PromptHandler = (confirmPw) =>
                        {
                            confirmPw = confirmPw.Trim();
                            if (confirmPw != newPw)
                            {
                                ExitPrompt("Passwords don't match. Password reset cancelled.");
                                return;
                            }

                            var newHash = BCrypt.Net.BCrypt.HashPassword(newPw);
                            _persistence.UpdatePasswordHash(session.PlayerEntity.Id, newHash);
                            _ = _persistence.SavePlayer(session);
                            ExitPrompt("Password updated.");
                        };
                    };
                };
                return;
            }

            if (ctx.Args.Length == 2)
            {
                // Admin reset
                if (!session.PlayerEntity.HasTag("admin"))
                {
                    _sessions.SendToPlayer(ctx.PlayerEntityId, "You don't have permission to do that.\r\n");
                    return;
                }

                var targetName = ctx.Args[0];
                var newPassword = ctx.Args[1];
                if (newPassword.Length < _config.Persistence.PasswordMinLength)
                {
                    _sessions.SendToPlayer(ctx.PlayerEntityId,
                        $"Password must be at least {_config.Persistence.PasswordMinLength} characters.\r\n");
                    return;
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);

                // Check if target is online
                var targetSession = _sessions.GetByPlayerName(targetName);
                if (targetSession != null)
                {
                    _persistence.UpdatePasswordHash(targetSession.PlayerEntity.Id, hash);
                    _ = _persistence.SavePlayer(targetSession);
                    _sessions.SendToPlayer(targetSession.PlayerEntity.Id,
                        "Your password has been reset by an administrator.\r\n");
                }
                else
                {
                    // Offline reset — load, update hash, save
                    var data = _persistence.LoadPlayer(targetName).GetAwaiter().GetResult();
                    if (data == null)
                    {
                        _sessions.SendToPlayer(ctx.PlayerEntityId, "Player not found.\r\n");
                        return;
                    }
                    _ = _persistence.SaveNewPlayer(data.Entity, hash);
                }

                _sessions.SendToPlayer(ctx.PlayerEntityId,
                    $"Password reset for {targetName}.\r\n");
            }
        }, priority: 100, packName: "core",
           description: "Change your password. Admins can reset another player's password.",
           category: "system");
    }

    private void LoadSeedPlayers()
    {
        var packsDir = Path.Combine(AppContext.BaseDirectory, "packs");

        foreach (var packName in _config.Packs)
        {
            if (!_packLoader.LoadedPacks.Any(p => p.Name == packName))
            {
                continue;
            }

            var playersPath = Path.Combine(packsDir, packName, "players.yaml");
            if (!File.Exists(playersPath))
            {
                continue;
            }

            var yaml = File.ReadAllText(playersPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var seedData = deserializer.Deserialize<SeedPlayersFile>(yaml);
            if (seedData?.Players == null)
            {
                continue;
            }

            foreach (var seed in seedData.Players)
            {
                if (_persistence.PlayerSaveExists(seed.Name))
                {
                    continue;
                }

                var entity = new Entity("player", seed.Name);
                foreach (var tag in seed.Tags)
                {
                    entity.AddTag(tag);
                }
                entity.Stats.BaseStrength = seed.Stats.Strength;
                entity.Stats.BaseIntelligence = seed.Stats.Intelligence;
                entity.Stats.BaseWisdom = seed.Stats.Wisdom;
                entity.Stats.BaseDexterity = seed.Stats.Dexterity;
                entity.Stats.BaseConstitution = seed.Stats.Constitution;
                entity.Stats.BaseLuck = seed.Stats.Luck;
                entity.Stats.BaseMaxHp = seed.Stats.MaxHp;
                entity.Stats.BaseMaxResource = seed.Stats.MaxResource;
                entity.Stats.BaseMaxMovement = seed.Stats.MaxMovement;
                entity.Stats.Hp = seed.Stats.MaxHp;
                entity.Stats.Resource = seed.Stats.MaxResource;
                entity.Stats.Movement = seed.Stats.MaxMovement;
                entity.SetProperty(CommonProperties.RegenHp, 2);
                entity.SetProperty(CommonProperties.RegenResource, 1);
                entity.SetProperty(CommonProperties.RegenMovement, 3);

                if (!string.IsNullOrEmpty(seed.PlayerClass)) {
                    entity.SetProperty(CommonProperties.Class, seed.PlayerClass);
                }

                if (!string.IsNullOrEmpty(seed.PlayerRace)) {
                    entity.SetProperty(CommonProperties.Race, seed.PlayerRace);
                    var raceDef = _raceRegistry.Get(seed.PlayerRace);
                    if (raceDef != null) {
                        foreach (var flag in raceDef.RacialFlags) { entity.AddTag(flag); }
                    }
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(seed.Password);
                _persistence.SaveNewPlayer(entity, hash).GetAwaiter().GetResult();

                _logger.LogInformation("Created seed player: {Name}", seed.Name);
            }
        }
    }

    private void RunInitialSpawns()
    {
        foreach (var areaName in _spawns.GetAreaNames())
        {
            _spawns.RunAreaReset(areaName);
        }
    }

    private class SeedPlayersFile
    {
        public List<SeedPlayer> Players { get; set; } = new();
    }

    private class SeedPlayer
    {
        public string Name { get; set; } = "";
        public string Password { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public string? PlayerClass { get; set; }
        public string? PlayerRace { get; set; }
        public SeedPlayerStats Stats { get; set; } = new();
    }

    private class SeedPlayerStats
    {
        public int Strength { get; set; } = 10;
        public int Intelligence { get; set; } = 10;
        public int Wisdom { get; set; } = 10;
        public int Dexterity { get; set; } = 10;
        public int Constitution { get; set; } = 10;
        public int Luck { get; set; } = 10;
        public int MaxHp { get; set; } = 100;
        public int MaxResource { get; set; } = 50;
        public int MaxMovement { get; set; } = 100;
    }
}
