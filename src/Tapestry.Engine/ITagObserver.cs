namespace Tapestry.Engine;

public interface ITagObserver
{
    void OnTagAdded(Entity entity, string tag);
    void OnTagRemoved(Entity entity, string tag);
}
