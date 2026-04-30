namespace Tapestry.Engine.Flow;

public class PlayerCreator
{
    private readonly Dictionary<Guid, Entity> _pending = new();

    public void TrackEntity(Entity entity)
    {
        _pending[entity.Id] = entity;
    }

    public Entity? GetEntity(Guid id)
    {
        _pending.TryGetValue(id, out var entity);
        return entity;
    }

    public void Remove(Guid id)
    {
        _pending.Remove(id);
    }

    public bool Contains(Guid id)
    {
        return _pending.ContainsKey(id);
    }

    public IEnumerable<Entity> All => _pending.Values;
}
