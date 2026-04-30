// See also: CommonProperties.cs for shared entity properties
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Abilities;

/// <summary>
/// Property keys for the ability system (proficiency tracking, queued actions, last used).
/// </summary>
public static class AbilityProperties
{
    /// <summary>Key prefix: "proficiency:"</summary>
    public const string ProficiencyPrefix = "proficiency:";

    /// <summary>Key: "queued_actions"</summary>
    public const string QueuedActions = "queued_actions";

    /// <summary>Key: "last_ability_used"</summary>
    public const string LastAbilityUsed = "last_ability_used";

    /// <summary>Key prefix: "cap:"</summary>
    public const string CapPrefix = "cap:";

    /// <summary>Key: "proficiency:{abilityId}"</summary>
    public static string Proficiency(string abilityId) => $"proficiency:{abilityId}";

    /// <summary>Key: "cap:{abilityId}"</summary>
    public static string Cap(string abilityId) => $"cap:{abilityId}";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.RegisterPrefix(ProficiencyPrefix, typeof(int));
        registry.RegisterPrefix(CapPrefix, typeof(int));
        registry.Register(LastAbilityUsed, typeof(string));
    }
}
