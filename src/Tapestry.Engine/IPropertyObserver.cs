namespace Tapestry.Engine;

/// <summary>Observes property-bag mutations on an <see cref="Entity"/>, mirroring ITagObserver.</summary>
public interface IPropertyObserver
{
    void OnPropertyChanged(Entity entity, string key, object? oldValue, object? newValue);
}
