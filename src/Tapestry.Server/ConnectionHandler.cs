using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Color;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Prompt;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Shared;

namespace Tapestry.Server;

public class ConnectionHandler
{
    private static readonly string DefaultRecallRoom = "core:town-square";

    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly SystemEventQueue _eventQueue;
    private readonly TapestryMetrics _metrics;
    private readonly PlayerPersistenceService _persistence;
    private readonly ServerConfig _config;
    private readonly ILogger<ConnectionHandler> _logger;
    private readonly FlowEngine _flowEngine;
    private readonly ColorRenderer _colorRenderer;
    private readonly LoginGateRegistry _loginGates;
    private readonly GmcpService _gmcpService;
    private readonly GameLoop _gameLoop;
    private readonly MobAIManager _mobAI;
    private readonly object _nameReservationLock = new();

    public ConnectionHandler(
        SessionManager sessions,
        World world,
        SystemEventQueue eventQueue,
        TapestryMetrics metrics,
        PlayerPersistenceService persistence,
        ServerConfig config,
        ILogger<ConnectionHandler> logger,
        FlowEngine flowEngine,
        ColorRenderer colorRenderer,
        LoginGateRegistry loginGates,
        GmcpService gmcpService,
        GameLoop gameLoop,
        MobAIManager mobAI)
    {
        _sessions = sessions;
        _world = world;
        _eventQueue = eventQueue;
        _metrics = metrics;
        _persistence = persistence;
        _config = config;
        _logger = logger;
        _flowEngine = flowEngine;
        _colorRenderer = colorRenderer;
        _loginGates = loginGates;
        _gmcpService = gmcpService;
        _gameLoop = gameLoop;
        _mobAI = mobAI;
        _flowEngine.NewPlayerEntityFactory = CreateNewPlayerEntity;
        _flowEngine.GmcpSend = (connectionId, package, payload) =>
            _gmcpService.SendRaw(connectionId, package, payload);
    }

    private static Entity CreateNewPlayerEntity(string name)
    {
        var entity = new Entity("player", name);
        entity.AddTag("player");
        entity.AddTag("regen");
        entity.Stats.BaseStrength = 10;
        entity.Stats.BaseIntelligence = 10;
        entity.Stats.BaseWisdom = 10;
        entity.Stats.BaseDexterity = 10;
        entity.Stats.BaseConstitution = 10;
        entity.Stats.BaseLuck = 10;
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.BaseMaxResource = 50;
        entity.Stats.BaseMaxMovement = 100;
        entity.Stats.Hp = 100;
        entity.Stats.Resource = 50;
        entity.Stats.Movement = 100;
        entity.SetProperty(CommonProperties.RegenHp, 2);
        entity.SetProperty(CommonProperties.RegenResource, 1);
        entity.SetProperty(CommonProperties.RegenMovement, 3);
        entity.SetProperty(PromptProperties.PromptTemplate, PromptRenderer.DefaultTemplate);
        return entity;
    }

    public void HandleNewConnection(IConnection rawConnection, IGmcpHandler? gmcpHandler)
    {
        if (gmcpHandler != null)
        {
            _gmcpService.RegisterHandler(rawConnection.Id, gmcpHandler);
            rawConnection.OnDisconnected += () =>
            {
                _gmcpService.UnregisterHandler(rawConnection.Id);
            };
        }

        _gmcpService.SendLoginPhase(rawConnection.Id, "name");

        IConnection connection = new ColorRenderingConnection(rawConnection, _colorRenderer);
        var failedAttempts = 0;
        var maxAttempts = _config.Persistence.MaxLoginAttempts;

        connection.SendLine("");
        connection.SendLine("=== " + _config.Server.Name + " ===");
        connection.SendLine("");
        connection.SendLine("What is your name, adventurer?");
        _gmcpService.SendLoginPrompt(rawConnection.Id, "What is your name, adventurer?");

        Action<string>? currentHandler = null;

        void SetHandler(Action<string> handler)
        {
            if (currentHandler != null)
            {
                connection.OnInput -= currentHandler;
            }
            currentHandler = handler;
            connection.OnInput += currentHandler;
        }

        void CompleteLogin(Entity entity)
        {
            if (currentHandler != null)
            {
                connection.OnInput -= currentHandler;
                currentHandler = null;
            }

            var spawnRoom = _world.GetRoom(entity.LocationRoomId ?? DefaultRecallRoom);
            if (spawnRoom == null)
            {
                spawnRoom = _world.GetRoom(DefaultRecallRoom);
            }
            if (spawnRoom == null)
            {
                spawnRoom = _world.AllRooms.FirstOrDefault();
            }

            if (spawnRoom != null)
            {
                spawnRoom.AddEntity(entity);
                _mobAI.PlayerEnteredRoom(spawnRoom.Id);
            }

            _world.TrackEntity(entity);

            var session = new PlayerSession(connection, entity);
            session.Phase = SessionPhase.Playing;
            _sessions.Add(session);
            _metrics.ActiveConnections.Add(1);

            _logger.LogInformation("Player {Name} connected (entity {Id})", entity.Name, entity.Id);

            connection.SendLine("");
            connection.SendLine("Welcome, " + entity.Name + "!");
            connection.SendLine("");

            session.InputQueue.Enqueue("motd");
            session.InputQueue.Enqueue("look");

            // Send login phase immediately so the client transitions before world data arrives.
            _gmcpService.SendLoginPhase(rawConnection.Id, "playing");

            // Schedule the rest of the GMCP burst on the game loop thread — Jint is not thread-safe
            // and SendCharCommands invokes JS visibleTo predicates via the scripting engine.
            var capturedConnectionId = rawConnection.Id;
            var capturedEntity = entity;
            _gameLoop.Schedule(() => _gmcpService.SendPostLoginBurst(capturedConnectionId, capturedEntity));

            connection.OnDisconnected += () =>
            {
                _eventQueue.Enqueue(new DisconnectEvent(
                    Guid.Parse(connection.Id), entity.Id, "connection closed"));
            };
        }

        Action<string> nameHandler = null!;
        nameHandler = (input) =>
        {
            var name = input.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                connection.SendLine("Please enter a name.");
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z]{2,20}$"))
            {
                connection.SendLine("Names must be 2-20 letters only.");
                connection.SendLine("What is your name, adventurer?");
                _gmcpService.SendLoginPrompt(rawConnection.Id, "What is your name, adventurer?");
                return;
            }

            if (_sessions.GetByPlayerName(name) != null && !_persistence.PlayerSaveExists(name))
            {
                connection.SendLine("Someone else is creating that name right now. Try another.");
                connection.SendLine("What is your name, adventurer?");
                _gmcpService.SendLoginPrompt(rawConnection.Id, "What is your name, adventurer?");
                return;
            }

            if (_persistence.PlayerSaveExists(name))
            {
                _gmcpService.SendLoginPhase(rawConnection.Id, "password");
                connection.SuppressEcho();
                connection.SendLine("Password:");
                _gmcpService.SendLoginPrompt(rawConnection.Id, "Password:");

                SetHandler((passwordInput) =>
                {
                    connection.RestoreEcho();
                    connection.SendLine("");
                    var password = passwordInput.Trim();
                    var data = _persistence.LoadPlayer(name).GetAwaiter().GetResult();

                    if (data == null)
                    {
                        connection.SendLine("Error loading character. Please try again.");
                        connection.SendLine("What is your name, adventurer?");
                        _gmcpService.SendLoginPhase(rawConnection.Id, "name");
                        _gmcpService.SendLoginPrompt(rawConnection.Id, "What is your name, adventurer?");
                        SetHandler(nameHandler);
                        return;
                    }

                    if (!BCrypt.Net.BCrypt.Verify(password, data.PasswordHash))
                    {
                        failedAttempts++;
                        if (failedAttempts >= maxAttempts)
                        {
                            connection.SendLine("Too many failed attempts.");
                            connection.Disconnect("login lockout");
                            return;
                        }
                        connection.SendLine("Incorrect password.");
                        connection.SendLine("What is your name, adventurer?");
                        _gmcpService.SendLoginPhase(rawConnection.Id, "name");
                        _gmcpService.SendLoginPrompt(rawConnection.Id, "What is your name, adventurer?");
                        SetHandler(nameHandler);
                        return;
                    }

                    var existingSession = _sessions.GetByPlayerName(name);
                    if (existingSession != null)
                    {
                        _gmcpService.SendLoginPhase(rawConnection.Id, "name");
                        connection.SendLine("That character is already connected. Reconnect? (y/n)");
                        _gmcpService.SendLoginPrompt(rawConnection.Id, "That character is already connected. Reconnect? (y/n)");

                        SetHandler((confirmInput) =>
                        {
                            var confirm = confirmInput.Trim().ToLowerInvariant();
                            if (confirm == "y" || confirm == "yes")
                            {
                                var liveEntity = existingSession.PlayerEntity;
                                existingSession.SendLine("Another connection has taken over this character.");
                                _sessions.Remove(existingSession);
                                existingSession.Connection.Disconnect("session takeover");
                                _metrics.ActiveConnections.Add(-1);

                                CompleteLogin(liveEntity);
                            }
                            else
                            {
                                connection.SendLine("What is your name, adventurer?");
                                _gmcpService.SendLoginPhase(rawConnection.Id, "name");
                                _gmcpService.SendLoginPrompt(rawConnection.Id, "What is your name, adventurer?");
                                SetHandler(nameHandler);
                            }
                        });
                        return;
                    }

                    var entity = data.Entity;

                    foreach (var (corpse, corpseItems) in data.Corpses)
                    {
                        if (corpse.LocationRoomId != null)
                        {
                            var corpseRoom = _world.GetRoom(corpse.LocationRoomId);
                            if (corpseRoom != null)
                            {
                                corpseRoom.AddEntity(corpse);
                                _world.TrackEntity(corpse);
                                foreach (var item in corpseItems)
                                {
                                    _world.TrackEntity(item);
                                }
                            }
                        }
                    }

                    foreach (var item in data.AllItems)
                    {
                        _world.TrackEntity(item);
                    }

                    CompleteLogin(entity);
                });
            }
            else
            {
                var gateResult = _loginGates.RunAll(name, connection);
                if (!gateResult.Allowed)
                {
                    if (gateResult.Message != null)
                    {
                        connection.SendLine(gateResult.Message);
                    }
                    if (gateResult.Behavior == LoginBlockBehavior.Disconnect)
                    {
                        connection.Disconnect("login gate");
                        return;
                    }
                    connection.SendLine("What is your name, adventurer?");
                    _gmcpService.SendLoginPrompt(rawConnection.Id, "What is your name, adventurer?");
                    return;
                }

                var creationAttempts = 0;

                _gmcpService.SendLoginPhase(rawConnection.Id, "password");
                connection.SuppressEcho();
                connection.SendLine("New character! Choose a password:");
                _gmcpService.SendLoginPrompt(rawConnection.Id, "New character! Choose a password:");

                Action<string> firstPasswordHandler = null!;
                firstPasswordHandler = (passwordInput) =>
                {
                    connection.SendLine("");
                    var password = passwordInput.Trim();
                    if (password.Length < _config.Persistence.PasswordMinLength)
                    {
                        creationAttempts++;
                        if (creationAttempts >= 3)
                        {
                            connection.RestoreEcho();
                            connection.SendLine("Too many failed attempts.");
                            connection.Disconnect("login failed");
                            return;
                        }
                        connection.SendLine(
                            $"Password must be at least {_config.Persistence.PasswordMinLength} characters.");
                        connection.SuppressEcho();
                        connection.SendLine("Choose a password:");
                        _gmcpService.SendLoginPrompt(rawConnection.Id, "Choose a password:");
                        return;
                    }

                    connection.SendLine("Confirm password:");
                    _gmcpService.SendLoginPrompt(rawConnection.Id, "Confirm password:");
                    SetHandler((confirmInput) =>
                    {
                        connection.SendLine("");
                        var confirm = confirmInput.Trim();
                        if (confirm != password)
                        {
                            creationAttempts++;
                            if (creationAttempts >= 3)
                            {
                                connection.RestoreEcho();
                                connection.SendLine("Too many failed attempts.");
                                connection.Disconnect("login failed");
                                return;
                            }
                            connection.SendLine("Passwords don't match. Try again.");
                            connection.SuppressEcho();
                            connection.SendLine("Choose a password:");
                            _gmcpService.SendLoginPrompt(rawConnection.Id, "Choose a password:");
                            SetHandler(firstPasswordHandler);
                            return;
                        }

                        connection.RestoreEcho();
                        var hash = BCrypt.Net.BCrypt.HashPassword(password);

                        var entity = CreateNewPlayerEntity(name);
                        var session = new PlayerSession(connection, entity)
                        {
                            Phase = SessionPhase.Creating,
                            PendingPasswordHash = hash
                        };

                        bool reserved;
                        lock (_nameReservationLock)
                        {
                            reserved = _sessions.GetByPlayerName(name) == null;
                            if (reserved)
                            {
                                _sessions.Add(session);
                            }
                        }

                        if (!reserved)
                        {
                            connection.SendLine("Someone else is creating that name right now. Try another.");
                            connection.SendLine("What is your name, adventurer?");
                            _gmcpService.SendLoginPrompt(rawConnection.Id, "What is your name, adventurer?");
                            SetHandler(nameHandler);
                            return;
                        }
                        _metrics.ActiveConnections.Add(1);

                        _logger.LogInformation(
                            "New player {Name} entering creation flow (entity {Id})", name, entity.Id);

                        connection.OnDisconnected += () =>
                        {
                            if (session.Phase == SessionPhase.Creating)
                            {
                                _sessions.Remove(session);
                                _metrics.ActiveConnections.Add(-1);
                                _logger.LogInformation("New player {Name} disconnected mid-creation", name);
                            }
                            else
                            {
                                _eventQueue.Enqueue(
                                    new DisconnectEvent(Guid.Parse(connection.Id), session.PlayerEntity.Id, "connection closed"));
                            }
                        };

                        if (currentHandler != null)
                        {
                            connection.OnInput -= currentHandler;
                            currentHandler = null;
                        }

                        _gmcpService.SendLoginPhase(rawConnection.Id, "creating");
                        _flowEngine.Trigger(session, "new_player_connect");
                    });
                };

                SetHandler(firstPasswordHandler);
            }
        };

        SetHandler(nameHandler);
    }
}
