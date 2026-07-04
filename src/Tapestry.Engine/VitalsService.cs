using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine;

/// <summary>
/// The single publishing write path for an entity's typed vitals (hp / resource / movement).
/// Every gameplay mutation goes through Apply/Set/RestoreToMax, which clamp and publish
/// <c>entity.vital.changed</c> on change. Initialize establishes a spawn/load baseline without
/// publishing. Vitals stay typed fields on <see cref="StatBlock"/>; this service is the seam
/// that makes "mutate" and "notify" the same act.
/// </summary>
public class VitalsService
{
    private readonly EventBus _eventBus;

    public VitalsService(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public int Apply(Entity entity, VitalKind kind, int delta, string reason)
    {
        return Set(entity, kind, ReadCurrent(entity.Stats, kind) + delta, reason);
    }

    public int Set(Entity entity, VitalKind kind, int value, string reason)
    {
        var oldValue = ReadCurrent(entity.Stats, kind);
        var newValue = entity.Stats.SetVital(kind, value);
        if (newValue != oldValue)
        {
            Publish(entity, kind, oldValue, newValue, reason);
        }
        return newValue;
    }

    public void RestoreToMax(Entity entity, string reason)
    {
        Set(entity, VitalKind.Hp, ReadMax(entity.Stats, VitalKind.Hp), reason);
        Set(entity, VitalKind.Resource, ReadMax(entity.Stats, VitalKind.Resource), reason);
        Set(entity, VitalKind.Movement, ReadMax(entity.Stats, VitalKind.Movement), reason);
    }

    public void Initialize(Entity entity, int hp, int resource, int movement)
    {
        entity.Stats.InitializeVitals(hp, resource, movement);
    }

    private void Publish(Entity entity, VitalKind kind, int oldValue, int newValue, string reason)
    {
        _eventBus.Publish(new GameEvent
        {
            Type = "entity.vital.changed",
            SourceEntityId = entity.Id,
            RoomId = entity.LocationRoomId,
            SourceEntityName = entity.Name,
            Data = new Dictionary<string, object?>
            {
                ["vital"] = TopicName(kind),
                ["old"] = oldValue,
                ["new"] = newValue,
                ["delta"] = newValue - oldValue,
                ["reason"] = reason
            }
        });
    }

    private static int ReadCurrent(StatBlock stats, VitalKind kind) => kind switch
    {
        VitalKind.Hp => stats.Hp,
        VitalKind.Resource => stats.Resource,
        VitalKind.Movement => stats.Movement,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown vital kind.")
    };

    private static int ReadMax(StatBlock stats, VitalKind kind) => kind switch
    {
        VitalKind.Hp => stats.MaxHp,
        VitalKind.Resource => stats.MaxResource,
        VitalKind.Movement => stats.MaxMovement,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown vital kind.")
    };

    private static string TopicName(VitalKind kind) => kind switch
    {
        VitalKind.Hp => "hp",
        VitalKind.Resource => "resource",
        VitalKind.Movement => "movement",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown vital kind.")
    };
}
