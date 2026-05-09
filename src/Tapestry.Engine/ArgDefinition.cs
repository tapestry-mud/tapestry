namespace Tapestry.Engine;

public class ArgDefinition
{
    public string Type { get; init; } = "keyword";   // inventory|room_item|entity|player|npc|player_global|container|direction|door|text|keyword|number
    public bool Required { get; init; } = true;
    public bool Bulk { get; init; } = false;          // inventory/room_item only
    public string[] Prepositions { get; init; } = []; // tokens to discard before this arg
    public bool BypassVisibility { get; init; } = false;
}

public class GmcpConfig
{
    public string? Channel { get; init; }             // null = "feedback" default
    public bool PrependSender { get; init; } = false;
    public bool Disabled { get; init; } = false;      // gmcp: false in JS
}
