using Microsoft.Extensions.Logging;
using Tapestry.Engine.Flow;

namespace Tapestry.Engine.Persistence;

public class PlayerPersistenceService
{
    private readonly IPlayerStore _store;
    private readonly PlayerSerializer _serializer;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly ILogger<PlayerPersistenceService> _logger;

    // Track password hashes for online players (not stored on Entity)
    private readonly Dictionary<Guid, string> _passwordHashes = new();

    public PlayerPersistenceService(
        IPlayerStore store,
        PlayerSerializer serializer,
        SessionManager sessions,
        World world,
        ILogger<PlayerPersistenceService> logger)
    {
        _store = store;
        _serializer = serializer;
        _sessions = sessions;
        _world = world;
        _logger = logger;
    }

    public void TrackPasswordHash(Guid entityId, string hash)
    {
        _passwordHashes[entityId] = hash;
    }

    public void UntrackPasswordHash(Guid entityId)
    {
        _passwordHashes.Remove(entityId);
    }

    public string? GetPasswordHash(Guid entityId)
    {
        return _passwordHashes.GetValueOrDefault(entityId);
    }

    public void UpdatePasswordHash(Guid entityId, string newHash)
    {
        _passwordHashes[entityId] = newHash;
    }

    public async Task SavePlayer(PlayerSession session)
    {
        var entity = session.PlayerEntity;
        var hash = _passwordHashes.GetValueOrDefault(entity.Id, "");

        var allItems = CollectPlayerItems(entity);
        var corpses = CollectPlayerCorpses(entity);

        var dto = _serializer.ToSaveData(entity, hash, allItems, corpses);

        await _store.SaveAsync(dto);
    }

    public async Task<PlayerLoadResult?> LoadPlayer(string name)
    {
        var data = await _store.LoadAsync(name);
        if (data == null)
        {
            return null;
        }

        var result = _serializer.FromSaveData(data);
        _passwordHashes[result.Entity.Id] = result.PasswordHash;
        return result;
    }

    public async Task SaveAllPlayers()
    {
        var count = 0;
        foreach (var session in _sessions.AllSessions.Where(s => s.Phase == SessionPhase.Playing))
        {
            try
            {
                await SavePlayer(session);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save player {Name}", session.PlayerEntity.Name);
            }
        }
        if (count > 0)
        {
            _logger.LogInformation("Autosaved {Count} players", count);
        }
    }

    public List<PlayerSaveData> SnapshotAllPlayers()
    {
        var snapshots = new List<PlayerSaveData>();
        foreach (var session in _sessions.AllSessions.Where(s => s.Phase == SessionPhase.Playing))
        {
            try
            {
                var entity = session.PlayerEntity;
                var hash = _passwordHashes.GetValueOrDefault(entity.Id, "");
                var allItems = CollectPlayerItems(entity);
                var corpses = CollectPlayerCorpses(entity);
                snapshots.Add(_serializer.ToSaveData(entity, hash, allItems, corpses));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to snapshot player {Name}", session.PlayerEntity.Name);
            }
        }
        return snapshots;
    }

    public async Task WriteSnapshotsAsync(List<PlayerSaveData> snapshots)
    {
        var count = 0;
        foreach (var dto in snapshots)
        {
            try
            {
                await _store.SaveAsync(dto);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write player save {Name}", dto.Name);
            }
        }
        if (count > 0)
        {
            _logger.LogInformation("Autosaved {Count} players", count);
        }
    }

    public bool PlayerSaveExists(string name)
    {
        return _store.Exists(name);
    }

    public async Task SaveNewPlayer(Entity entity, string passwordHash)
    {
        _passwordHashes[entity.Id] = passwordHash;
        var allItems = CollectPlayerItems(entity);
        var dto = _serializer.ToSaveData(entity, passwordHash, allItems, new List<(Entity, List<Entity>)>());
        await _store.SaveAsync(dto);
    }

    private List<Entity> CollectPlayerItems(Entity player)
    {
        var items = new List<Entity>();
        CollectItemsRecursive(player.Contents, items);
        foreach (var kvp in player.Equipment)
        {
            if (!items.Contains(kvp.Value))
            {
                items.Add(kvp.Value);
            }
        }
        return items;
    }

    private void CollectItemsRecursive(IReadOnlyList<Entity> contents, List<Entity> items)
    {
        foreach (var item in contents)
        {
            items.Add(item);
            CollectItemsRecursive(item.Contents, items);
        }
    }

    private List<(Entity Corpse, List<Entity> Items)> CollectPlayerCorpses(Entity player)
    {
        var corpses = new List<(Entity, List<Entity>)>();

        foreach (var room in _world.AllRooms)
        {
            foreach (var entity in room.Entities)
            {
                if (entity.HasTag("player_corpse") &&
                    entity.Name == "corpse of " + player.Name)
                {
                    var corpseItems = new List<Entity>();
                    CollectItemsRecursive(entity.Contents, corpseItems);
                    corpses.Add((entity, corpseItems));
                }
            }
        }

        return corpses;
    }
}
