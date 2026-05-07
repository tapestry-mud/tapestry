using Tapestry.Shared;

namespace Tapestry.Engine;

public class EventBus
{
    private readonly Dictionary<string, SortedList<int, List<Subscription>>> _handlers = new();
    private readonly List<Subscription> _wildcardHandlers = new();
    private readonly Dictionary<string, Subscription[]> _cache = new();
    private readonly object _sync = new();
    private int _nextId;

    public int Subscribe(string eventType, Action<GameEvent> handler, int priority = 0)
    {
        var id = Interlocked.Increment(ref _nextId);
        var sub = new Subscription(id, handler, priority);

        if (eventType == "*")
        {
            lock (_sync)
            {
                _wildcardHandlers.Add(sub);
                _cache.Clear();
            }
            return id;
        }

        lock (_sync)
        {
            if (!_handlers.TryGetValue(eventType, out var priorityMap))
            {
                priorityMap = new SortedList<int, List<Subscription>>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
                _handlers[eventType] = priorityMap;
            }

            if (!priorityMap.TryGetValue(priority, out var list))
            {
                list = new List<Subscription>();
                priorityMap[priority] = list;
            }
            list.Add(sub);
            _cache.Remove(eventType);
        }

        return id;
    }

    public void Unsubscribe(int subscriptionId)
    {
        lock (_sync)
        {
            foreach (var (eventType, priorityMap) in _handlers)
            {
                var found = false;
                foreach (var list in priorityMap.Values)
                {
                    if (list.RemoveAll(s => s.Id == subscriptionId) > 0)
                    {
                        found = true;
                    }
                }
                if (found)
                {
                    _cache.Remove(eventType);
                    break;
                }
            }

            if (_wildcardHandlers.RemoveAll(s => s.Id == subscriptionId) > 0)
            {
                _cache.Clear();
            }
        }
    }

    public void Publish(GameEvent evt)
    {
        var handlers = GetHandlers(evt.Type);

        for (var i = 0; i < handlers.Length; i++)
        {
            if (evt.Cancelled)
            {
                break;
            }
            handlers[i].Handler(evt);
        }
    }

    private Subscription[] GetHandlers(string eventType)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(eventType, out var cached))
            {
                return cached;
            }
            var result = BuildMergedHandlers(eventType);
            _cache[eventType] = result;
            return result;
        }
    }

    private Subscription[] BuildMergedHandlers(string eventType)
    {
        var merged = new SortedList<int, List<Subscription>>(Comparer<int>.Create((a, b) => b.CompareTo(a)));

        if (_handlers.TryGetValue(eventType, out var priorityMap))
        {
            foreach (var (priority, subs) in priorityMap)
            {
                if (!merged.TryGetValue(priority, out var list))
                {
                    list = new List<Subscription>();
                    merged[priority] = list;
                }
                list.AddRange(subs);
            }
        }

        foreach (var sub in _wildcardHandlers)
        {
            if (!merged.TryGetValue(sub.Priority, out var list))
            {
                list = new List<Subscription>();
                merged[sub.Priority] = list;
            }
            list.Add(sub);
        }

        return merged.Values.SelectMany(l => l).ToArray();
    }

    private record Subscription(int Id, Action<GameEvent> Handler, int Priority);
}
