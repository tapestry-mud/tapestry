using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Flow;

namespace Tapestry.Engine.Persistence;

public class PlayerPersistenceService
{
    private readonly IPlayerStore _store;
    private readonly PlayerSerializer _serializer;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly ILogger<PlayerPersistenceService> _logger;

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

    public async Task SavePlayer(PlayerSession session)
    {
        var entity = session.PlayerEntity;
        var accountId = session.AccountId;

        var allItems = CollectPlayerItems(entity);
        var dto = _serializer.ToSaveData(entity, accountId, allItems);

        await _store.SaveAsync(dto);
    }

    public async Task<PlayerLoadResult?> LoadPlayer(string name)
    {
        var data = await _store.LoadAsync(name);
        if (data == null)
        {
            return null;
        }

        return _serializer.FromSaveData(data);
    }

    public async Task SaveAllPlayers()
    {
        var count = 0;
        foreach (var session in _sessions.AllSessions.Where(s =>
            s.Phase == LoginPhase.Playing || s.Phase == LoginPhase.LinkDead))
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
        foreach (var session in _sessions.AllSessions.Where(s =>
            s.Phase == LoginPhase.Playing || s.Phase == LoginPhase.LinkDead))
        {
            try
            {
                var entity = session.PlayerEntity;
                var allItems = CollectPlayerItems(entity);
                snapshots.Add(_serializer.ToSaveData(entity, session.AccountId, allItems));
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

    public async Task SaveNewPlayer(Entity entity, Guid accountId)
    {
        var allItems = CollectPlayerItems(entity);
        var dto = _serializer.ToSaveData(entity, accountId, allItems);
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
}
