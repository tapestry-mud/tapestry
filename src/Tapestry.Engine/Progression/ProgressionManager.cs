using Tapestry.Shared;

namespace Tapestry.Engine.Progression;

public class ProgressionManager
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly Dictionary<string, TrackDefinition> _tracks = new();

    public ProgressionManager(World world, EventBus eventBus)
    {
        _world = world;
        _eventBus = eventBus;
    }

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

        var level = entity.GetProperty<int>(ProgressionProperties.Level(trackName));
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

        var level = entity.GetProperty<int>(ProgressionProperties.Level(trackName));
        if (level == 0)
        {
            InitializeTrack(entity, trackName);
            level = 1;
        }

        var xp = entity.GetProperty<int>(ProgressionProperties.Xp(trackName));
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

        var level = entity.GetProperty<int>(ProgressionProperties.Level(trackName));
        if (level == 0)
        {
            InitializeTrack(entity, trackName);
            level = 1;
        }

        var currentXp = entity.GetProperty<int>(ProgressionProperties.Xp(trackName));
        var newXp = currentXp + amount;
        entity.SetProperty(ProgressionProperties.Xp(trackName), newXp);

        // Fire XP gained event
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

        // Check for level-ups
        while (level < track.MaxLevel)
        {
            var nextThreshold = track.GetXpForLevel(level + 1);
            if (nextThreshold < 0 || newXp < nextThreshold)
            {
                break;
            }

            var oldLevel = level;
            level++;
            entity.SetProperty(ProgressionProperties.Level(trackName), level);

            // Fire callback if defined
            track.OnLevelUp?.Invoke(entityId, trackName, level);

            // Fire level-up event
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

        var level = entity.GetProperty<int>(ProgressionProperties.Level(trackName));
        if (level == 0)
        {
            return;
        }

        var currentXp = entity.GetProperty<int>(ProgressionProperties.Xp(trackName));
        var floor = level <= 1 ? 0 : track.GetXpForLevel(level);
        if (floor < 0)
        {
            floor = 0;
        }

        var newXp = Math.Max(floor, currentXp - amount);
        var actualLoss = currentXp - newXp;
        entity.SetProperty(ProgressionProperties.Xp(trackName), newXp);

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

        entity.SetProperty(ProgressionProperties.Level(trackName), 1);
        entity.SetProperty(ProgressionProperties.Xp(trackName), 0);

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
        entity.SetProperty(ProgressionProperties.Level(trackName), 1);
        entity.SetProperty(ProgressionProperties.Xp(trackName), 0);
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
