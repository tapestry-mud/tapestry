// src/Tapestry.Engine/Distribution/SpawnOnEntry.cs
namespace Tapestry.Engine.Distribution;

/// <summary>
/// One entry in an item template's spawn_on block: a selector + placement policy.
/// Chance is resolved from Rarity before storage (Rarity is not carried here).
/// </summary>
public record SpawnOnEntry(SelectorSpec Selector, double Chance, int Count)
{
    /// <summary>Named rarity tiers map to canonical chance values.</summary>
    public static double RarityToChance(string rarity) => rarity.ToLowerInvariant() switch
    {
        "common"    => 0.90,
        "uncommon"  => 0.50,
        "rare"      => 0.15,
        "very_rare" => 0.05,
        _ => throw new ArgumentException($"Unknown rarity tier '{rarity}'")
    };
}
