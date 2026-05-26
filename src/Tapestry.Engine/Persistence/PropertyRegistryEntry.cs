namespace Tapestry.Engine.Persistence;

public sealed record PropertyRegistryEntry(
    string Name,
    string Scope,
    string Description,
    PropertyValueType ValueType,
    IReadOnlySet<string>? AppliesTo,
    bool Transient,
    double? Min = null,
    double? Max = null,
    IReadOnlySet<string>? Enum = null
)
{
    public string FullName => Scope == "engine" ? Name : $"{Scope}:{Name}";
    public bool IsEngineProperty => Scope == "engine";
    public bool AppliesToType(string entityType) =>
        AppliesTo == null || AppliesTo.Contains(entityType);
}
