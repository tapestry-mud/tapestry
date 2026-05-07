using System.Collections.Concurrent;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Login;
using Tapestry.Engine.Prompt;
using Tapestry.Shared;

namespace Tapestry.Engine;

public enum InputMode { Normal, Prompt }

public class PlayerSession
{
    public const int MaxQueueDepth = 100;

    public IConnection Connection { get; }
    public Entity PlayerEntity { get; private set; }
    public ConcurrentQueue<string> InputQueue { get; } = new();
    public LoginPhase Phase { get; set; } = LoginPhase.Creating;
    public FlowInstance? CurrentFlow { get; set; }
    public string? PendingPasswordHash { get; set; }

    internal void ReplaceEntity(Entity entity)
    {
        PlayerEntity = entity;
    }
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;
    public long LastInputTick { get; private set; }
    public bool IdleWarned { get; set; }
    public bool PromptDisplayed { get; set; }
    public bool NeedsPromptRefresh { get; set; }
    public bool ReceivedInput { get; set; }
    public string? LastCommand { get; set; }
    public InputMode InputMode { get; set; } = InputMode.Normal;
    public Action<string>? PromptHandler { get; set; }
    public Action? CancelPreLoginTimeout { get; set; }

    public void UpdateLastInputTick(long tick)
    {
        LastInputTick = tick;
        IdleWarned = false;
    }

    public bool EnqueueInput(string input)
    {
        if (InputQueue.Count >= MaxQueueDepth)
        {
            return false;
        }
        InputQueue.Enqueue(input);
        return true;
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
        EnqueueInput(input);
    }

    public PlayerSession(IConnection connection, Entity playerEntity)
    {
        Connection = connection;
        PlayerEntity = playerEntity;

        connection.OnInput += (input) =>
        {
            HandleInput(input.Trim());
        };
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
    private readonly ConcurrentDictionary<string, LoginContext> _preLogin = new();

    public void Add(PlayerSession session)
    {
        _byConnectionId[session.Connection.Id] = session;
        _byEntityId[session.PlayerEntity.Id] = session;
        _byPlayerName[session.PlayerEntity.Name.ToLowerInvariant()] = session;
    }

    public void Remove(PlayerSession session)
    {
        _byConnectionId.TryRemove(session.Connection.Id, out _);
        _byEntityId.TryRemove(session.PlayerEntity.Id, out _);
        _byPlayerName.TryRemove(session.PlayerEntity.Name.ToLowerInvariant(), out _);
    }

    public void UpdateEntityId(Guid oldEntityId, PlayerSession session)
    {
        _byEntityId.TryRemove(oldEntityId, out _);
        _byEntityId[session.PlayerEntity.Id] = session;
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

    public IEnumerable<PlayerSession> AllSessions => _byConnectionId.Values;

    public void SendToTag(string tag, string text)
    {
        foreach (var session in AllSessions)
        {
            if (session.PlayerEntity.HasTag(tag))
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
