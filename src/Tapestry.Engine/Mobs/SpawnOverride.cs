namespace Tapestry.Engine.Mobs;

/// <summary>A frozen blob captured at generation time and re-applied verbatim on every
/// (re)spawn. Carries both halves of the dice/LLM line: identity (name/desc, LLM) and
/// facts (max_hp/damage/items, dice). NoReroll suppresses the stat re-roll, the rare-swap,
/// and loot re-roll so the same instance returns after a reset.</summary>
public sealed class SpawnOverride
{
    public string? FromType { get; set; }
    public string? Name { get; set; }
    public string? Desc { get; set; }
    public int? MaxHp { get; set; }
    public string? Damage { get; set; }
    public List<string> Items { get; set; } = new();
    public bool NoReroll { get; set; }
}
