namespace Tapestry.Contracts;

public interface IDirtyVitalsBatcher
{
    void MarkDirty(Guid entityId);
}
