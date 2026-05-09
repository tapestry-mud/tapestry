namespace Tapestry.Engine.Tags;

public sealed record TagRegistryEntry(
    string Name,
    string Scope,
    string Description,
    IReadOnlySet<string> AppliesTo
)
{
    public string FullName => Scope == "engine" ? Name : $"{Scope}:{Name}";
    public bool IsEngineTag => Scope == "engine";
    public bool AppliesToType(string entityType) =>
        AppliesTo.Contains(entityType);
}
