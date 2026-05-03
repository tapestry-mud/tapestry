using System.Collections.Concurrent;

namespace Tapestry.Scripting.Services;

public class CommandResponseContext
{
    private readonly ConcurrentDictionary<Guid, byte> _suppressed = new();

    public void Suppress(Guid entityId)
    {
        _suppressed[entityId] = 0;
    }

    public bool IsSuppressed(Guid entityId)
    {
        return _suppressed.ContainsKey(entityId);
    }

    public void Reset(Guid entityId)
    {
        _suppressed.TryRemove(entityId, out _);
    }
}
