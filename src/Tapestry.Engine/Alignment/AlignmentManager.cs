// src/Tapestry.Engine/Alignment/AlignmentManager.cs
using Tapestry.Shared;

namespace Tapestry.Engine.Alignment;

public class AlignmentManager
{
    private const int MinValue = -1000;
    private const int MaxValue = 1000;
    private const int HistoryCapacity = 20;

    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly AlignmentConfig _config;

    public AlignmentManager(World world, EventBus eventBus, AlignmentConfig config)
    {
        _world = world;
        _eventBus = eventBus;
        _config = config;
    }

    public int Get(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        return entity?.GetProperty<int?>("alignment") ?? 0;
    }

    public string Bucket(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return "neutral"; }
        var alignment = entity.GetProperty<int?>("alignment") ?? 0;
        var bucket = _config.BucketFor(alignment);
        SetBucketTag(entity, bucket);
        return bucket;
    }

    public IReadOnlyList<AlignmentHistoryEntry> History(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return Array.Empty<AlignmentHistoryEntry>(); }
        return entity.GetProperty<List<AlignmentHistoryEntry>>("alignment_history")
            ?? (IReadOnlyList<AlignmentHistoryEntry>)Array.Empty<AlignmentHistoryEntry>();
    }

    // Hard-set: no events fired, no admin check. Useful for admin commands and testing.
    public void Set(Guid entityId, int value, string reason)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        var clamped = Math.Clamp(value, MinValue, MaxValue);
        entity.SetProperty("alignment", clamped);
        SetBucketTag(entity, _config.BucketFor(clamped));
    }

    // Fires alignment.shift.check (cancellable). Optional context dict is merged into
    // the event payload — use it to pass caller-specific fields like targetId (CombatManager)
    // or questId (future quest hooks). Single code path for all callers.
    public void Shift(Guid entityId, int delta, string reason,
                      IDictionary<string, object?>? context = null)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        if (entity.HasTag("admin")) { return; }

        var eventData = new Dictionary<string, object?>
        {
            ["actorId"] = entityId.ToString(),
            ["reason"] = reason,
            ["suggestedDelta"] = (object)delta,
            ["cancel"] = (object)false
        };
        if (context != null)
        {
            foreach (var kv in context) { eventData[kv.Key] = kv.Value; }
        }

        var checkEvent = new GameEvent
        {
            Type = "alignment.shift.check",
            SourceEntityId = entityId,
            RoomId = entity.LocationRoomId,
            Data = eventData
        };
        _eventBus.Publish(checkEvent);

        if (checkEvent.Data["cancel"] is true) { return; }

        var rawDelta = checkEvent.Data["suggestedDelta"];
        var resolvedDelta = rawDelta is int i ? i
            : rawDelta is double d ? (int)d
            : delta;

        if (resolvedDelta == 0) { return; }
        ApplyShift(entityId, resolvedDelta, reason);
    }

    private void ApplyShift(Guid entityId, int delta, string reason)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }

        var oldValue = entity.GetProperty<int?>("alignment") ?? 0;
        var newValue = Math.Clamp(oldValue + delta, MinValue, MaxValue);
        var actualDelta = newValue - oldValue;
        if (actualDelta == 0) { return; }

        entity.SetProperty("alignment", newValue);

        var oldBucket = _config.BucketFor(oldValue);
        var newBucket = _config.BucketFor(newValue);
        SetBucketTag(entity, newBucket);
        AppendHistory(entity, actualDelta, reason, newValue);

        _eventBus.Publish(new GameEvent
        {
            Type = "alignment.shifted",
            SourceEntityId = entityId,
            Data = new Dictionary<string, object?>
            {
                ["entityId"] = entityId.ToString(),
                ["oldValue"] = (object)oldValue,
                ["newValue"] = (object)newValue,
                ["delta"] = (object)actualDelta,
                ["reason"] = reason,
                ["bucketChanged"] = (object)(oldBucket != newBucket)
            }
        });

        if (oldBucket != newBucket)
        {
            _eventBus.Publish(new GameEvent
            {
                Type = "alignment.bucket.changed",
                SourceEntityId = entityId,
                Data = new Dictionary<string, object?>
                {
                    ["entityId"] = entityId.ToString(),
                    ["oldBucket"] = oldBucket,
                    ["newBucket"] = newBucket
                }
            });
        }
    }

    private void SetBucketTag(Entity entity, string bucket)
    {
        entity.RemoveTag("alignment_evil");
        entity.RemoveTag("alignment_neutral");
        entity.RemoveTag("alignment_good");
        entity.AddTag($"alignment_{bucket}");
    }

    private static void AppendHistory(Entity entity, int delta, string reason, int newValue)
    {
        var history = entity.GetProperty<List<AlignmentHistoryEntry>>("alignment_history")
            ?? new List<AlignmentHistoryEntry>();
        history.Add(new AlignmentHistoryEntry(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), delta, reason, newValue));
        while (history.Count > HistoryCapacity) { history.RemoveAt(0); }
        entity.SetProperty("alignment_history", history);
    }
}
