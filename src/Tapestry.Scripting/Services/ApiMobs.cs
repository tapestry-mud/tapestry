using Tapestry.Engine;
using Tapestry.Engine.Mobs;

namespace Tapestry.Scripting.Services;

public class ApiMobs
{
    private readonly World _world;
    private readonly MobAIManager _mobAIManager;
    private readonly SpawnManager _spawnManager;

    public ApiMobs(World world, MobAIManager mobAIManager, SpawnManager spawnManager)
    {
        _world = world;
        _mobAIManager = mobAIManager;
        _spawnManager = spawnManager;
    }

    public Dictionary<string, object?>? GetEntityProperties(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return null;
        }

        var entity = _world.GetEntity(entityId);
        if (entity == null)
        {
            return null;
        }

        return new Dictionary<string, object?>(entity.GetAllProperties());
    }

    public long GetMobTicksSinceLastAction(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return 0;
        }

        return _mobAIManager.GetTicksSinceLastAction(entityId);
    }

    public void RecordMobAction(string entityIdStr)
    {
        if (Guid.TryParse(entityIdStr, out var entityId))
        {
            _mobAIManager.RecordAction(entityId);
        }
    }

    public object? SpawnMob(string templateId, string roomId)
    {
        var entity = _spawnManager.SpawnMob(templateId, roomId);
        if (entity == null)
        {
            return null;
        }

        return new { id = entity.Id.ToString(), name = entity.Name };
    }
}
