using Tapestry.Shared;

namespace Tapestry.Engine;

public class EventBus
{
    private readonly Dictionary<string, SortedList<int, List<Subscription>>> _handlers = new();
    private readonly List<Subscription> _wildcardHandlers = new();
    private int _nextId;

    public int Subscribe(string eventType, Action<GameEvent> handler, int priority = 0)
    {
        var id = Interlocked.Increment(ref _nextId);
        var sub = new Subscription(id, handler, priority);

        if (eventType == "*")
        {
            lock (_wildcardHandlers)
            {
                _wildcardHandlers.Add(sub);
            }
            return id;
        }

        lock (_handlers)
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
        }

        return id;
    }

    public void Unsubscribe(int subscriptionId)
    {
        lock (_handlers)
        {
            foreach (var priorityMap in _handlers.Values)
            {
                foreach (var list in priorityMap.Values)
                {
                    list.RemoveAll(s => s.Id == subscriptionId);
                }
            }
        }

        lock (_wildcardHandlers)
        {
            _wildcardHandlers.RemoveAll(s => s.Id == subscriptionId);
        }
    }

    public void Publish(GameEvent evt)
    {
        var handlers = GetHandlers(evt.Type);

        foreach (var handler in handlers)
        {
            if (evt.Cancelled)
            {
                break;
            }
            handler.Handler(evt);
        }
    }

    private IEnumerable<Subscription> GetHandlers(string eventType)
    {
        var result = new SortedList<int, List<Subscription>>(Comparer<int>.Create((a, b) => b.CompareTo(a)));

        lock (_handlers)
        {
            if (_handlers.TryGetValue(eventType, out var priorityMap))
            {
                foreach (var (priority, subs) in priorityMap)
                {
                    if (!result.TryGetValue(priority, out var list))
                    {
                        list = new List<Subscription>();
                        result[priority] = list;
                    }
                    list.AddRange(subs);
                }
            }
        }

        lock (_wildcardHandlers)
        {
            foreach (var sub in _wildcardHandlers)
            {
                if (!result.TryGetValue(sub.Priority, out var list))
                {
                    list = new List<Subscription>();
                    result[sub.Priority] = list;
                }
                list.Add(sub);
            }
        }

        return result.Values.SelectMany(list => list);
    }

    private record Subscription(int Id, Action<GameEvent> Handler, int Priority);
}
