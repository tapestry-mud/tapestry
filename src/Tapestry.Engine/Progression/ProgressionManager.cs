using Tapestry.Engine.Quests;
using Tapestry.Shared;

namespace Tapestry.Engine.Progression;

public class ProgressionManager : IQuestProgressionService
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly Dictionary<string, TrackDefinition> _tracks = new();

    public ProgressionManager(World world, EventBus eventBus)
    {
        _world = world;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Plain write (upsert). Collision resolution is the RegistrationPolicy's job — by the
    /// time a write lands here, the policy has already elected the winner.
    /// </summary>
    public void RegisterTrack(TrackDefinition track)
    {
        _tracks[track.Name] = track;
    }

    public TrackDefinition? GetTrackDefinition(string trackName)
    {
        if (_tracks.TryGetValue(trackName, out var def))
        {
            return def;
        }
        return null;
    }

    public IReadOnlyList<TrackDefinition> GetAllTracks()
    {
        return _tracks.Values.ToList().AsReadOnly();
    }

    public int GetLevel(Guid entityId, string trackName)
    {
        if (!_tracks.ContainsKey(trackName))
        {
            return 0;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return 0;
        }

        var level = GetTrackInt(entity, ProgressionProperties.Level, trackName);
        if (level == 0)
        {
            InitializeTrack(entity, trackName);
            return 1;
        }
        return level;
    }

    public TrackInfo? GetTrackInfo(Guid entityId, string trackName)
    {
        if (!_tracks.TryGetValue(trackName, out var track))
        {
            return null;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return null;
        }

        var level = GetTrackInt(entity, ProgressionProperties.Level, trackName);
        if (level == 0)
        {
            InitializeTrack(entity, trackName);
            level = 1;
        }

        var xp = GetTrackInt(entity, ProgressionProperties.Xp, trackName);
        var currentThreshold = level <= 1 ? 0 : track.GetXpForLevel(level);
        if (currentThreshold < 0)
        {
            currentThreshold = 0;
        }
        var nextThreshold = track.GetXpForLevel(level + 1);
        var xpToNext = nextThreshold >= 0 ? nextThreshold - xp : 0;

        var overflow = 0;
        if (level >= track.MaxLevel)
        {
            overflow = xp - currentThreshold;
            xpToNext = 0;
        }

        return new TrackInfo(
            Xp: xp,
            Level: level,
            XpToNext: Math.Max(0, xpToNext),
            CurrentLevelThreshold: Math.Max(0, currentThreshold),
            MaxLevel: track.MaxLevel,
            Overflow: overflow
        );
    }

    public void GrantExperience(Guid entityId, int amount, string trackName, string source)
    {
        if (!_tracks.TryGetValue(trackName, out var track))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }

        var level = GetTrackInt(entity, ProgressionProperties.Level, trackName);
        if (level == 0)
        {
            InitializeTrack(entity, trackName);
            level = 1;
        }

        var currentXp = GetTrackInt(entity, ProgressionProperties.Xp, trackName);
        var newXp = currentXp + amount;
        SetTrackInt(entity, ProgressionProperties.Xp, trackName, newXp);

        _eventBus.Publish(new GameEvent
        {
            Type = "progression.xp.gained",
            SourceEntityId = entityId,
            Data = new Dictionary<string, object?>
            {
                ["track"] = trackName,
                ["amount"] = amount,
                ["source"] = source,
                ["newTotal"] = newXp
            }
        });

        while (level < track.MaxLevel)
        {
            var nextThreshold = track.GetXpForLevel(level + 1);
            if (nextThreshold < 0 || newXp < nextThreshold)
            {
                break;
            }

            var oldLevel = level;
            level++;
            SetTrackInt(entity, ProgressionProperties.Level, trackName, level);

            track.OnLevelUp?.Invoke(entityId, trackName, level);

            _eventBus.Publish(new GameEvent
            {
                Type = "progression.level.up",
                SourceEntityId = entityId,
                Data = new Dictionary<string, object?>
                {
                    ["track"] = trackName,
                    ["oldLevel"] = oldLevel,
                    ["newLevel"] = level,
                    ["entityId"] = entityId.ToString()
                }
            });
        }
    }

    public void DeductExperience(Guid entityId, int amount, string trackName)
    {
        if (!_tracks.TryGetValue(trackName, out var track))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }

        var level = GetTrackInt(entity, ProgressionProperties.Level, trackName);
        if (level == 0)
        {
            return;
        }

        var currentXp = GetTrackInt(entity, ProgressionProperties.Xp, trackName);
        var floor = level <= 1 ? 0 : track.GetXpForLevel(level);
        if (floor < 0)
        {
            floor = 0;
        }

        var newXp = Math.Max(floor, currentXp - amount);
        var actualLoss = currentXp - newXp;
        SetTrackInt(entity, ProgressionProperties.Xp, trackName, newXp);

        if (actualLoss > 0)
        {
            _eventBus.Publish(new GameEvent
            {
                Type = "progression.xp.lost",
                SourceEntityId = entityId,
                Data = new Dictionary<string, object?>
                {
                    ["track"] = trackName,
                    ["amount"] = actualLoss,
                    ["newTotal"] = newXp
                }
            });
        }
    }

    public void ResetTrack(Guid entityId, string trackName)
    {
        if (!_tracks.ContainsKey(trackName))
        {
            return;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return;
        }

        SetTrackInt(entity, ProgressionProperties.Level, trackName, 1);
        SetTrackInt(entity, ProgressionProperties.Xp, trackName, 0);

        _eventBus.Publish(new GameEvent
        {
            Type = "progression.track.reset",
            SourceEntityId = entityId,
            Data = new Dictionary<string, object?>
            {
                ["track"] = trackName,
                ["entityId"] = entityId.ToString()
            }
        });
    }

    private void InitializeTrack(Entity entity, string trackName)
    {
        SetTrackInt(entity, ProgressionProperties.Level, trackName, 1);
        SetTrackInt(entity, ProgressionProperties.Xp, trackName, 0);
    }

    private static int GetTrackInt(Entity entity, string propertyKey, string trackName)
    {
        var map = entity.GetProperty<Dictionary<string, int>>(propertyKey);
        if (map == null || !map.TryGetValue(trackName, out var value))
        {
            return 0;
        }
        return value;
    }

    private static void SetTrackInt(Entity entity, string propertyKey, string trackName, int value)
    {
        var map = entity.GetProperty<Dictionary<string, int>>(propertyKey) ?? new Dictionary<string, int>();
        map[trackName] = value;
        entity.SetProperty(propertyKey, map);
    }
}

public record TrackInfo(
    int Xp,
    int Level,
    int XpToNext,
    int CurrentLevelThreshold,
    int MaxLevel,
    int Overflow
);
