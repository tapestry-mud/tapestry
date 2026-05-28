// src/Tapestry.Engine/Distribution/SpawnOnEntry.cs
namespace Tapestry.Engine.Distribution;

/// <summary>
/// Controls whether a spawn_on entry's Count is applied per-room or as a world-wide budget.
/// </summary>
public enum SpawnScope
{
    /// <summary>Default: replenish each matching room to Count independently.</summary>
    Room,
    /// <summary>Count is the total across ALL matching rooms; placement chooses rooms randomly.</summary>
    Global
}

/// <summary>
/// One entry in an item template's spawn_on block: a selector + placement policy.
/// Chance is resolved from Rarity before storage (Rarity is not carried here).
/// </summary>
public record SpawnOnEntry(SelectorSpec Selector, double Chance, int Count, SpawnScope Scope = SpawnScope.Room)
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
