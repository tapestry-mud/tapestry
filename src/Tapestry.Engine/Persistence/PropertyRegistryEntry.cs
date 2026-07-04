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
    IReadOnlySet<string>? EnumValues = null,
    bool Settable = true,
    bool Observable = false,
    string? ChangeTopic = null
)
{
    public string FullName => Scope == "engine" ? Name : $"{Scope}:{Name}";
    public bool IsEngineProperty => Scope == "engine";
    public bool AppliesToType(string entityType) =>
        AppliesTo == null || AppliesTo.Contains(entityType);

    public bool IsCollectionType =>
        ValueType is PropertyValueType.MapInt or PropertyValueType.MapString or PropertyValueType.ListString;

    // Not hand-settable via the admin `set` command when explicitly flagged unsettable (Settable: false,
    // e.g. template_id) or transient (engine-managed runtime bookkeeping). Still in the registry for
    // type/serialization metadata and introspection.
    public bool IsAdminSettable => Settable && !Transient;
}
