// src/Tapestry.Engine/Mobs/MobCommandQueue.cs
using Microsoft.Extensions.Logging;

namespace Tapestry.Engine.Mobs;

public class MobCommandQueue
{
    private readonly World _world;
    private readonly MobCommandRegistry _registry;
    private readonly TickTimer _timer;
    private readonly ILogger<MobCommandQueue> _logger;

    // Per-mob: the tick at which the last queued command is scheduled to fire.
    // New commands chain from here rather than from the current tick.
    private readonly Dictionary<Guid, long> _lastScheduledTick = new();

    // Sorted by fire tick. Multiple commands can share the same tick.
    private readonly SortedDictionary<long, List<QueuedCommand>> _queue = new();

    public MobCommandQueue(World world, MobCommandRegistry registry,
        TickTimer timer, ILogger<MobCommandQueue> logger)
    {
        _world = world;
        _registry = registry;
        _timer = timer;
        _logger = logger;
    }

    public void Enqueue(Guid entityId, string commandStr, double delaySeconds)
    {
        var now = _timer.CurrentTick;
        var baseTick = _lastScheduledTick.TryGetValue(entityId, out var last) && last > now ? last : now;
        var fireTick = baseTick + _timer.SecondsToTicks(delaySeconds);
        _lastScheduledTick[entityId] = fireTick;

        if (!_queue.TryGetValue(fireTick, out var bucket))
        {
            bucket = new List<QueuedCommand>();
            _queue[fireTick] = bucket;
        }
        bucket.Add(new QueuedCommand(entityId, commandStr));
    }

    public void ProcessTick()
    {
        var now = _timer.CurrentTick;
        var readyKeys = _queue.Keys.TakeWhile(k => k <= now).ToList();

        foreach (var key in readyKeys)
        {
            var bucket = _queue[key];
            _queue.Remove(key);

            foreach (var cmd in bucket)
            {
                var entity = _world.GetEntity(cmd.EntityId);
                if (entity == null)
                {
                    continue;
                }

                try
                {
                    _registry.Dispatch(cmd.EntityId, cmd.CommandStr);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MobCommandQueue dispatch error: entity={EntityId}", cmd.EntityId);
                }
            }
        }
    }

    // Test helper: returns scheduled fire ticks for all queued commands for a given entity, in order.
    public List<long> GetScheduledTicks(Guid entityId)
    {
        return _queue
            .SelectMany(kvp => kvp.Value.Where(c => c.EntityId == entityId).Select(_ => kvp.Key))
            .OrderBy(t => t)
            .ToList();
    }

    private record QueuedCommand(Guid EntityId, string CommandStr);
}
