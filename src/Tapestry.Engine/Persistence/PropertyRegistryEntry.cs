namespace Tapestry.Engine.Persistence;

public sealed record PropertyRegistryEntry(
    string Name,
    string Scope,
    string Description,
    PropertyValueType ValueType,
    IReadOnlySet<string>? AppliesTo,
    bool Transient
)
{
    public string FullName => Scope == "engine" ? Name : $"{Scope}:{Name}";
    public bool IsEngineProperty => Scope == "engine";
    public bool AppliesToType(string entityType) =>
        AppliesTo == null || AppliesTo.Contains(entityType);
}
