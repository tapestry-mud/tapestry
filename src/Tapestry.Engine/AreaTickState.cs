namespace Tapestry.Engine;

public class AreaTickState
{
    public string AreaId { get; init; } = "";
    public int TickCount { get; set; }
    public long TicksSinceLastFire { get; set; }
    public int? OverrideResetInterval { get; set; }
    public float? OverrideOccupiedModifier { get; set; }
}
