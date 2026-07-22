using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Login;
using Tapestry.Engine.Prompt;
using Tapestry.Shared;

namespace Tapestry.Engine;

public enum InputMode { Normal, Prompt }

public class PlayerSession
{
    public const int MaxQueueDepth = 100;

    public IConnection Connection { get; private set; }
    public Entity PlayerEntity { get; private set; }
    private readonly ConcurrentQueue<string> _inputQueue = new();
    public int InputQueueCount => _inputQueue.Count;
    public LoginPhase Phase { get; set; } = LoginPhase.Creating;
    public Guid AccountId { get; }
    public FlowInstance? CurrentFlow { get; set; }
    public string? PendingPasswordHash { get; set; }

    internal void ReplaceEntity(Entity entity)
    {
        PlayerEntity = entity;
    }

    public void ReplaceConnection(IConnection newConnection)
    {
        Connection.OnInput -= _inputHandler;
        Connection = newConnection;
        newConnection.OnInput += _inputHandler;
    }

    public void ClearInputQueue()
    {
        while (_inputQueue.TryDequeue(out _)) { }
    }
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;
    public long LastInputTick { get; private set; }
    public long LinkDeadSinceTick { get; set; }
    public bool IdleWarned { get; set; }
    public bool PromptDisplayed { get; set; }
    public bool NeedsPromptRefresh { get; set; }
    public bool ReceivedInput { get; set; }
    public string? LastCommand { get; set; }
    public InputMode InputMode { get; set; } = InputMode.Normal;
    public Action<string>? PromptHandler { get; set; }
    public Action? CancelPreLoginTimeout { get; set; }

    private readonly HashSet<string> _promptHoldOwners = new();

    /// <summary>True while any owner (a swell, a cutscene) holds the prompt back. Consulted
    /// by SessionManager.FlushPrompts; the redraw decision is not per-owner, it is "any owner
    /// present or not."</summary>
    public bool IsPromptHeld => _promptHoldOwners.Count > 0;

    public void OpenPromptHold(string owner)
    {
        _promptHoldOwners.Add(owner);
    }

    /// <summary>Releasing the last owner arms exactly one redraw defensively, in case the
    /// hold ended with no trailing content to arm it naturally.</summary>
    public void ReleasePromptHold(string owner)
    {
        if (_promptHoldOwners.Remove(owner) && _promptHoldOwners.Count == 0)
        {
            NeedsPromptRefresh = true;
        }
    }

    /// <summary>Lifecycle-exit safety net: disconnect and link-death call this so a hold can
    /// never outlive the session that opened it, regardless of which owner opened it.</summary>
    public void ForceReleaseAllPromptHolds()
    {
        if (_promptHoldOwners.Count == 0) { return; }
        _promptHoldOwners.Clear();
        NeedsPromptRefresh = true;
    }

    private readonly Action<string> _inputHandler;
    private readonly FloodContext? _floodCtx;
    private float _tokens;
    private long _lastReplenishTick = -1;
    private int _floodStrikes;
    private long _lastStrikeTick;
    private bool _floodWarned;
    private bool _disconnected;

    public void UpdateLastInputTick(long tick)
    {
        LastInputTick = tick;
        IdleWarned = false;
    }

    public bool EnqueueInput(string input)
    {
        if (_inputQueue.Count >= MaxQueueDepth)
        {
            return false;
        }
        _inputQueue.Enqueue(input);
        return true;
    }

    public bool TryDequeueInput(out string? input)
    {
        return _inputQueue.TryDequeue(out input);
    }

    // OnInput fires serially per connection -- both read loops await before invoking.
    private bool TryConsumeToken()
    {
        if (_floodCtx == null) { return true; }
        if (_disconnected) { return false; }

        var currentTick = _floodCtx.GetCurrentTick();

        if (_lastReplenishTick < 0)
        {
            _tokens = _floodCtx.Config.BurstSize;
            _lastReplenishTick = currentTick;
        }

        var elapsed = currentTick - _lastReplenishTick;
        if (elapsed > 0)
        {
            var elapsedSeconds = (double)elapsed / _floodCtx.TicksPerSecond;
            _tokens = Math.Min(_floodCtx.Config.BurstSize,
                _tokens + (float)(elapsedSeconds * _floodCtx.Config.CommandsPerSecond));
            _lastReplenishTick = currentTick;
        }

        if (_floodStrikes > 0)
        {
            var decayTicks = (long)(_floodCtx.Config.StrikeDecaySeconds * _floodCtx.TicksPerSecond);
            if (currentTick - _lastStrikeTick >= decayTicks)
            {
                _floodStrikes = 0;
                _floodWarned = false;
            }
        }

        if (_tokens >= 1.0f)
        {
            _tokens -= 1.0f;
            return true;
        }

        if (!_floodWarned)
        {
            Send("Slow down.\r\n");
            _floodWarned = true;
        }

        _floodStrikes++;
        _lastStrikeTick = currentTick;

        _floodCtx.DroppedCounter?.Add(1,
            new KeyValuePair<string, object?>("player_name", PlayerEntity.Name));

        if (_floodStrikes >= _floodCtx.Config.StrikeThreshold)
        {
            _floodCtx.Logger?.LogWarning(
                "Player {PlayerName} ({EntityId}) disconnected: command flooding",
                PlayerEntity.Name, PlayerEntity.Id);
            _floodCtx.DisconnectCounter?.Add(1);
            Connection.SendLine("Disconnected: command flooding.");
            _disconnected = true;
            Connection.Disconnect("command flooding");
        }

        return false;
    }

    public void HandleInput(string input)
    {
        if (InputMode == InputMode.Prompt)
        {
            PromptHandler?.Invoke(input);
            return;
        }
        if (CurrentFlow != null)
        {
            if (CurrentFlow.Definition.Cancellable &&
                (input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                 input.Equals("cancel", StringComparison.OrdinalIgnoreCase)))
            {
                CurrentFlow = null;
                Send("Cancelled.\r\n");
                EnqueueInput("look");
                return;
            }
            CurrentFlow.HandleInput(input);
            return;
        }
        if (!TryConsumeToken())
        {
            return;
        }
        EnqueueInput(input);
    }

    public PlayerSession(IConnection connection, Entity playerEntity,
        Guid accountId = default, FloodContext? floodContext = null)
    {
        Connection = connection;
        PlayerEntity = playerEntity;
        AccountId = accountId;
        _floodCtx = floodContext;

        _inputHandler = (input) => HandleInput(input.Trim());
        connection.OnInput += _inputHandler;
    }

    public void Send(string text)
    {
        if (Connection.IsConnected)
        {
            Connection.SendText(text);
        }
    }

    public void SendLine(string text)
    {
        if (Connection.IsConnected)
        {
            Connection.SendLine(text);
        }
    }
}

public class SessionManager
{
    private readonly ConcurrentDictionary<string, PlayerSession> _byConnectionId = new();
    private readonly ConcurrentDictionary<Guid, PlayerSession> _byEntityId = new();
    private readonly ConcurrentDictionary<string, PlayerSession> _byPlayerName = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, List<PlayerSession>> _byAccountId = new();
    private readonly ConcurrentDictionary<string, LoginContext> _preLogin = new();

    public void Add(PlayerSession session)
    {
        var name = session.PlayerEntity.Name.ToLowerInvariant();
        if (_byPlayerName.TryGetValue(name, out var stale) &&
            stale.Connection.Id != session.Connection.Id)
        {
            _byConnectionId.TryRemove(stale.Connection.Id, out _);
        }

        _byConnectionId[session.Connection.Id] = session;
        _byEntityId[session.PlayerEntity.Id] = session;
        _byPlayerName[name] = session;

        if (session.AccountId != Guid.Empty)
        {
            var list = _byAccountId.GetOrAdd(session.AccountId, _ => new List<PlayerSession>());
            lock (list)
            {
                if (!list.Contains(session))
                {
                    list.Add(session);
                }
            }
        }
    }

    public void Remove(PlayerSession session)
    {
        _byConnectionId.TryRemove(session.Connection.Id, out _);
        _byEntityId.TryRemove(session.PlayerEntity.Id, out _);
        _byPlayerName.TryRemove(session.PlayerEntity.Name.ToLowerInvariant(), out _);

        if (session.AccountId != Guid.Empty &&
            _byAccountId.TryGetValue(session.AccountId, out var list))
        {
            lock (list)
            {
                list.Remove(session);
                if (list.Count == 0)
                {
                    _byAccountId.TryRemove(session.AccountId, out _);
                }
            }
        }
    }

    public void RemoveConnectionOnly(PlayerSession session)
    {
        _byConnectionId.TryRemove(session.Connection.Id, out _);
    }

    public void UpdateEntityId(Guid oldEntityId, PlayerSession session)
    {
        _byEntityId.TryRemove(oldEntityId, out _);
        _byEntityId[session.PlayerEntity.Id] = session;
    }

    public void ReRegisterConnectionForSession(PlayerSession session)
    {
        _byConnectionId[session.Connection.Id] = session;
    }

    public PlayerSession? GetByConnectionId(string connectionId)
    {
        return _byConnectionId.GetValueOrDefault(connectionId);
    }

    public PlayerSession? GetByEntityId(Guid entityId)
    {
        return _byEntityId.GetValueOrDefault(entityId);
    }

    public PlayerSession? GetByPlayerName(string name)
    {
        return _byPlayerName.GetValueOrDefault(name.ToLowerInvariant());
    }

    public IReadOnlyList<PlayerSession> GetByAccountId(Guid accountId)
    {
        if (_byAccountId.TryGetValue(accountId, out var list))
        {
            lock (list)
            {
                return list.ToList();
            }
        }
        return Array.Empty<PlayerSession>();
    }

    public int ActiveCharacterCount(Guid accountId, Guid? excludeEntityId = null)
    {
        if (!_byAccountId.TryGetValue(accountId, out var list))
        {
            return 0;
        }

        lock (list)
        {
            if (excludeEntityId.HasValue)
            {
                return list.Count(s => s.PlayerEntity.Id != excludeEntityId.Value);
            }
            return list.Count;
        }
    }

    public bool IsAccountAtCharacterLimit(
        Guid accountId, int maxConcurrent, Guid? excludeEntityId = null)
    {
        return ActiveCharacterCount(accountId, excludeEntityId) >= maxConcurrent;
    }

    public IEnumerable<PlayerSession> AllSessions => _byConnectionId.Values;

    public IEnumerable<PlayerSession> AllLinkDeadSessions =>
        _byEntityId.Values.Where(s => s.Phase == LoginPhase.LinkDead);

    /// <summary>Distinct room ids that hold at least one logged-in player. A cheap
    /// occupancy source (O(online players)) so callers can drive per-room work from the
    /// few populated rooms instead of scanning the whole world.</summary>
    public IEnumerable<string> OccupiedRoomIds()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in _byEntityId.Values)
        {
            var roomId = session.PlayerEntity.LocationRoomId;
            if (roomId != null && seen.Add(roomId))
            {
                yield return roomId;
            }
        }
    }

    public void SendToTag(string tag, string text)
    {
        foreach (var session in AllSessions)
        {
            if (session.PlayerEntity.HasRole(tag) || session.PlayerEntity.HasTag(tag))
            {
                session.Send(text);
            }
        }
    }

    public int Count => _byConnectionId.Count;

    public void RegisterPreLogin(LoginContext ctx)
    {
        _preLogin[ctx.ConnectionId] = ctx;
    }

    public void RemovePreLogin(string connectionId)
    {
        _preLogin.TryRemove(connectionId, out _);
    }

    public LoginContext? GetPreLogin(string connectionId)
    {
        return _preLogin.GetValueOrDefault(connectionId);
    }

    public IEnumerable<LoginContext> AllPreLoginConnections => _preLogin.Values;

    public IEnumerable<LoginContext> AllConnectionsByPhase(LoginPhase phase)
    {
        return _preLogin.Values.Where(c => c.Phase == phase);
    }

    public int ConnectionCount => _preLogin.Count + _byConnectionId.Count;

    private void SendContentToSession(PlayerSession session, string rendered)
    {
        if (session.PromptDisplayed && !session.ReceivedInput)
        {
            session.Send("\r\n");
        }
        session.PromptDisplayed = false;
        session.ReceivedInput = false;
        session.Send(rendered);
        session.NeedsPromptRefresh = true;
    }

    public void SendToPlayer(Guid entityId, string text)
    {
        var session = GetByEntityId(entityId);
        if (session != null)
        {
            SendContentToSession(session, text);
        }
    }

    public void SendToAll(string text, Guid? excludeId = null)
    {
        foreach (var session in _byEntityId.Values)
        {
            if (session.PlayerEntity.Id != excludeId)
            {
                SendContentToSession(session, text);
            }
        }
    }

    public void SendToRoom(string roomId, string text, Guid? excludeEntityId = null)
    {
        foreach (var session in _byEntityId.Values)
        {
            if (session.PlayerEntity.LocationRoomId == roomId &&
                session.PlayerEntity.Id != excludeEntityId)
            {
                SendContentToSession(session, text);
            }
        }
    }

    public void SendToRoom(string roomId, string text, IReadOnlySet<Guid> excludeEntityIds)
    {
        foreach (var session in _byEntityId.Values)
        {
            if (session.PlayerEntity.LocationRoomId == roomId &&
                !excludeEntityIds.Contains(session.PlayerEntity.Id))
            {
                SendContentToSession(session, text);
            }
        }
    }

    public void FlushPrompts(PromptRenderer promptRenderer)
    {
        foreach (var session in _byEntityId.Values)
        {
            if (session.Phase == LoginPhase.Creating) { continue; }
            if (session.Phase == LoginPhase.LinkDead) { continue; }
            if (session.InputMode == InputMode.Prompt) { continue; }
            if (session.CurrentFlow != null) { continue; }
            if (session.NeedsPromptRefresh)
            {
                var template = session.PlayerEntity.GetProperty<string>(PromptProperties.PromptTemplate)
                               ?? PromptRenderer.DefaultTemplate;
                var prompt = promptRenderer.Render(template, session.PlayerEntity);
                session.Send("\r\n" + prompt);
                session.NeedsPromptRefresh = false;
                session.PromptDisplayed = true;
            }
        }
    }
}
