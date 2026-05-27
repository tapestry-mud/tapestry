// src/Tapestry.Engine/Distribution/SelectorSpec.cs
namespace Tapestry.Engine.Distribution;

/// <summary>
/// The targeting half of a spawn_on entry. Exactly one of Id/Type/Tag/Shop is set.
/// </summary>
public record SelectorSpec(
    string? Id = null,
    string? Type = null,
    string? Tag = null,
    bool Shop = false)
{
    public bool HasTargetingKey => Id != null || Type != null || Tag != null || Shop;
}
