namespace Tapestry.Engine;

public abstract class SystemEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public class DisconnectEvent : SystemEvent
{
    public Guid SessionId { get; }
    public Guid EntityId { get; }
    public string Reason { get; }

    public DisconnectEvent(Guid sessionId, Guid entityId, string reason)
    {
        SessionId = sessionId;
        EntityId = entityId;
        Reason = reason;
    }
}

public class ConnectEvent : SystemEvent
{
    public Guid SessionId { get; }
    public Guid EntityId { get; }
    public string SpawnRoomId { get; }

    public ConnectEvent(Guid sessionId, Guid entityId, string spawnRoomId)
    {
        SessionId = sessionId;
        EntityId = entityId;
        SpawnRoomId = spawnRoomId;
    }
}
