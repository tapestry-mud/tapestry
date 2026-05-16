using System.Collections.Concurrent;

namespace Tapestry.Engine;

public record Notification(
    string Type,
    int Priority,
    string Text,
    string? GmcpPackage = null,
    object? GmcpPayload = null
);

public class NotificationQueue
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<Notification>> _queues = new();

    public void Enqueue(Guid entityId, Notification notification)
    {
        var queue = _queues.GetOrAdd(entityId, _ => new ConcurrentQueue<Notification>());
        queue.Enqueue(notification);
    }

    public List<Notification> DrainFor(Guid entityId)
    {
        if (!_queues.TryGetValue(entityId, out var queue))
        {
            return [];
        }

        var result = new List<Notification>();
        while (queue.TryDequeue(out var notification))
        {
            result.Add(notification);
        }

        result.Sort((a, b) =>
        {
            var cmp = a.Priority.CompareTo(b.Priority);
            return cmp != 0 ? cmp : 0; // stable sort preserves insertion order within same priority
        });

        return result;
    }
}
