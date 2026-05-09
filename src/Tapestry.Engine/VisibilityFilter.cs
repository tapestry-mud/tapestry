namespace Tapestry.Engine;

public class VisibilityFilter
{
    public bool CanSee(Entity observer, Entity candidate)
    {
        return true;
    }

    public IEnumerable<Entity> GetVisibleEntities(Room room, Entity? observer)
    {
        return room.Entities;
    }
}
