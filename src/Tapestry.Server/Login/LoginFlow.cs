using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Prompt;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Server.Login;

public partial class LoginFlow
{
    private readonly AsyncConnectionAdapter _adapter;
    private readonly LoginContext _context;
    private readonly PlayerPersistenceService _persistence;
    private readonly SessionManager _sessions;
    private readonly LoginGateRegistry _loginGates;
    private readonly LoginHandler? _loginHandler;
    private readonly ServerConfig _config;
    private readonly ILogger<LoginFlow> _logger;
    private readonly TapestryMetrics _metrics;
    private readonly FlowEngine? _flowEngine;
    private readonly object _nameReservationLock = new();

    private const string NamePrompt = "What will your name be, this turn of the Wheel?";
    private const string NameGmcpPrompt = "Type the name you will go by for this turn of the Wheel";

    [GeneratedRegex(@"^[a-zA-Z]{2,20}$")]
    private static partial Regex NamePattern();

    public LoginFlow(
        AsyncConnectionAdapter adapter,
        LoginContext context,
        PlayerPersistenceService persistence,
        SessionManager sessions,
        LoginGateRegistry loginGates,
        LoginHandler? loginHandler,
        ServerConfig config,
        ILogger<LoginFlow> logger,
        TapestryMetrics metrics,
        FlowEngine? flowEngine = null)
    {
        _adapter = adapter;
        _context = context;
        _persistence = persistence;
        _sessions = sessions;
        _loginGates = loginGates;
        _loginHandler = loginHandler;
        _config = config;
        _logger = logger;
        _metrics = metrics;
        _flowEngine = flowEngine;
    }

    public async Task RunAsync(PlayerSpawner spawner)
    {
        try
        {
            await RunLoginSequenceAsync(spawner);
        }
        catch (OperationCanceledException)
        {
            if (_adapter.IsConnected)
            {
                _adapter.SendLine("Connection timed out.");
                _adapter.Disconnect("pre-login timeout");
            }
            _sessions.RemovePreLogin(_context.ConnectionId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login flow error for connection {Id}", _context.ConnectionId);
            _sessions.RemovePreLogin(_context.ConnectionId);
            _adapter.Disconnect("login error");
        }
    }

    private async Task RunLoginSequenceAsync(PlayerSpawner spawner)
    {
        SetPhase(LoginPhase.Name);

        _adapter.SendLine("");
        _adapter.SendLine("=== " + _config.Server.Name + " ===");
        _adapter.SendLine("");
        _adapter.SendLine(NamePrompt);
        SendGmcpPrompt(NameGmcpPrompt);

        while (true)
        {
            var ct = _context.PhaseCts.Token;
            var raw = await _adapter.ReadLineAsync(ct);
            var trimmed = raw.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                _adapter.SendLine("Please enter a name.");
                SendGmcpPrompt(NameGmcpPrompt);
                continue;
            }

            if (!NamePattern().IsMatch(trimmed))
            {
                _adapter.SendLine("Names must be 2-20 letters only.");
                _adapter.SendLine(NamePrompt);
                SendGmcpPrompt(NameGmcpPrompt);
                continue;
            }

            var name = char.ToUpper(trimmed[0]) + trimmed[1..].ToLower();

            if (_persistence != null && _persistence.PlayerSaveExists(name))
            {
                var handled = await HandleExistingPlayerAsync(name, spawner);
                if (handled)
                {
                    return;
                }
            }
            else
            {
                var handled = await HandleNewPlayerAsync(name, spawner);
                if (handled)
                {
                    return;
                }
            }

            SetPhase(LoginPhase.Name);
            _adapter.SendLine(NamePrompt);
            SendGmcpPrompt(NameGmcpPrompt);
        }
    }

    private async Task<bool> HandleExistingPlayerAsync(string name, PlayerSpawner spawner)
    {
        SetPhase(LoginPhase.Password);
        _adapter.SuppressEcho();
        _adapter.SendLine("Password:");
        SendGmcpPrompt("Enter your password");

        var failedAttempts = 0;
        var maxAttempts = _config.Persistence.MaxLoginAttempts;

        while (true)
        {
            var ct = _context.PhaseCts.Token;
            var passwordInput = await _adapter.ReadLineAsync(ct);
            _adapter.RestoreEcho();
            _adapter.SendLine("");

            var password = passwordInput.Trim();
            var data = await _persistence.LoadPlayer(name);

            if (data == null)
            {
                _adapter.SendLine("Error loading character. Please try again.");
                return false;
            }

            if (!BCrypt.Net.BCrypt.Verify(password, data.PasswordHash))
            {
                failedAttempts++;
                if (failedAttempts >= maxAttempts)
                {
                    _adapter.SendLine("Too many failed attempts.");
                    _adapter.Disconnect("login lockout");
                    return true;
                }
                _adapter.SendLine("Incorrect password.");
                _adapter.SuppressEcho();
                _adapter.SendLine("Password:");
                SendGmcpPrompt("Enter your password");
                continue;
            }

            var existingSession = _sessions.GetByPlayerName(name);
            if (existingSession != null)
            {
                return await HandleSessionTakeoverAsync(existingSession, spawner);
            }

            spawner.RestoreWorldObjects(data);
            spawner.CompleteLogin(data.Entity, _context.Connection, _context);
            return true;
        }
    }

    private async Task<bool> HandleSessionTakeoverAsync(PlayerSession existing, PlayerSpawner spawner)
    {
        SetPhase(LoginPhase.SessionTakeover);
        _adapter.SendLine("That character is already connected. Reconnect? (y/n)");
        SendGmcpPrompt("Character already connected. Reconnect?");

        var ct = _context.PhaseCts.Token;
        var confirm = (await _adapter.ReadLineAsync(ct)).Trim().ToLowerInvariant();

        if (confirm is "y" or "yes")
        {
            spawner.TakeOverSession(existing, _context.Connection, _context);
            return true;
        }

        return false;
    }

    private async Task<bool> HandleNewPlayerAsync(string name, PlayerSpawner spawner)
    {
        if (_loginGates != null)
        {
            var gateResult = _loginGates.RunAll(name, _context.Connection);
            if (!gateResult.Allowed)
            {
                if (gateResult.Message != null)
                {
                    _adapter.SendLine(gateResult.Message);
                }
                if (gateResult.Behavior == LoginBlockBehavior.Disconnect)
                {
                    _adapter.Disconnect("login gate");
                    return true;
                }
                return false;
            }
        }

        SetPhase(LoginPhase.Password);
        _adapter.SuppressEcho();
        _adapter.SendLine("New character! Choose a password:");
        SendGmcpPrompt("Choose a password for your new character");

        var creationAttempts = 0;
        string? password = null;

        while (true)
        {
            var ct = _context.PhaseCts.Token;
            var passwordInput = await _adapter.ReadLineAsync(ct);
            _adapter.SendLine("");
            var candidate = passwordInput.Trim();

            if (candidate.Length < _config.Persistence.PasswordMinLength)
            {
                creationAttempts++;
                if (creationAttempts >= 3)
                {
                    _adapter.RestoreEcho();
                    _adapter.SendLine("Too many failed attempts.");
                    _adapter.Disconnect("login failed");
                    return true;
                }
                _adapter.SendLine($"Password must be at least {_config.Persistence.PasswordMinLength} characters.");
                _adapter.SendLine("Choose a password:");
                SendGmcpPrompt("Choose a password:");
                continue;
            }

            _adapter.SendLine("Confirm password:");
            SendGmcpPrompt("Confirm your password");

            var confirmInput = await _adapter.ReadLineAsync(ct);
            _adapter.SendLine("");
            var confirm = confirmInput.Trim();

            if (confirm != candidate)
            {
                creationAttempts++;
                if (creationAttempts >= 3)
                {
                    _adapter.RestoreEcho();
                    _adapter.SendLine("Too many failed attempts.");
                    _adapter.Disconnect("login failed");
                    return true;
                }
                _adapter.SendLine("Passwords don't match. Try again.");
                _adapter.SendLine("Choose a password:");
                SendGmcpPrompt("Choose a password:");
                continue;
            }

            password = candidate;
            break;
        }

        _adapter.RestoreEcho();
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        var entity = CreateNewPlayerEntity(name);
        var session = new PlayerSession(_context.Connection, entity)
        {
            Phase = LoginPhase.Creating,
            PendingPasswordHash = hash
        };

        bool reserved;
        lock (_nameReservationLock)
        {
            reserved = _sessions.GetByPlayerName(name) == null;
            if (reserved)
            {
                _sessions.RemovePreLogin(_context.ConnectionId);
                _sessions.Add(session);
            }
        }

        if (!reserved)
        {
            _adapter.SendLine("Someone else is creating that name right now. Try another.");
            return false;
        }

        _metrics.ActiveConnections.Add(1);

        _logger.LogInformation("New player {Name} entering creation flow (entity {Id})", name, entity.Id);

        session.CancelPreLoginTimeout = () => _context.PhaseCts.Cancel();

        _context.Connection.OnDisconnected += () =>
        {
            if (session.Phase == LoginPhase.Creating)
            {
                _sessions.Remove(session);
                _metrics.ActiveConnections.Add(-1);
                _logger.LogInformation("New player {Name} disconnected mid-creation", name);
            }
        };

        SetPhase(LoginPhase.Creating);
        _flowEngine?.Trigger(session, "new_player_connect");
        return true;
    }

    private void SetPhase(LoginPhase phase)
    {
        _context.PhaseCts.Cancel();
        _context.PhaseCts = new CancellationTokenSource();
        _context.Phase = phase;

        var timeoutSec = GetPhaseTimeout(phase);
        if (timeoutSec > 0)
        {
            _context.PhaseCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));
        }

        SendGmcpLoginPhase(phase.ToString().ToLower());
    }

    private int GetPhaseTimeout(LoginPhase phase)
    {
        var pt = _config.Idle.PhaseTimeouts;
        var fallback = _config.Idle.PreLoginTimeoutSeconds;
        return phase switch
        {
            LoginPhase.Name => pt.Name > 0 ? pt.Name : fallback,
            LoginPhase.Password => pt.Password > 0 ? pt.Password : fallback,
            LoginPhase.SessionTakeover => pt.SessionTakeover > 0 ? pt.SessionTakeover : fallback,
            LoginPhase.Creating => pt.Creating > 0 ? pt.Creating : fallback,
            _ => fallback
        };
    }

    private void SendGmcpPrompt(string prompt)
    {
        _loginHandler?.SendLoginPrompt(_context.ConnectionId, prompt);
    }

    private void SendGmcpLoginPhase(string phase)
    {
        _loginHandler?.SendLoginPhase(_context.ConnectionId, phase);
    }

    public static Entity CreateNewPlayerEntity(string name)
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
}
