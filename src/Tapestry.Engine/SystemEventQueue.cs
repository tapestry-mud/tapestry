using System.Collections.Concurrent;

namespace Tapestry.Engine;

public class SystemEventQueue
{
    private readonly ConcurrentQueue<SystemEvent> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(SystemEvent evt)
    {
        _queue.Enqueue(evt);
    }

    public List<SystemEvent> DrainAll()
    {
        var events = new List<SystemEvent>();
        while (_queue.TryDequeue(out var evt))
        {
            events.Add(evt);
        }
        return events;
    }
}
