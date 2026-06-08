namespace Tapestry.Engine;

/// <summary>
/// One snapshot of world content size, sampled on demand for diagnostics metrics.
/// Lets a runaway name its own dimension: which entity type grew, whether the tag
/// index or property bags ballooned.
/// </summary>
public sealed class WorldCensus
{
    public Dictionary<string, int> EntitiesByType { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public int TagCount { get; set; }            // distinct tag keys in the index
    public int TagMemberships { get; set; }      // sum of entity memberships across tag sets
    public long PropertiesTotal { get; set; }    // sum of property-bag entry counts
    public int MaxEntityProperties { get; set; } // largest single entity property bag
}
