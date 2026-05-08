using Tapestry.Contracts;

namespace Tapestry.Server.Gmcp;

public class DirtyVitalsBatcher : IDirtyVitalsBatcher
{
    private readonly HashSet<Guid> _dirty = new();
    private readonly object _lock = new();
    private Action<Guid>? _flushCallback;

    public void SetFlushCallback(Action<Guid> callback)
    {
        _flushCallback = callback;
    }

    public void MarkDirty(Guid entityId)
    {
        lock (_lock) { _dirty.Add(entityId); }
    }

    public void FlushDirtyVitals()
    {
        if (_flushCallback == null) { return; }
        Guid[] dirty;
        lock (_lock)
        {
            if (_dirty.Count == 0) { return; }
            dirty = _dirty.ToArray();
            _dirty.Clear();
        }
        foreach (var id in dirty) { _flushCallback(id); }
    }
}
