// src/Tapestry.Engine/Mobs/LootTable.cs
namespace Tapestry.Engine.Mobs;

public class LootGuaranteed
{
    public string Item { get; set; } = "";
    public int Count { get; set; } = 1;
}

public class LootPoolEntry
{
    public string Item { get; set; } = "";
    public int Weight { get; set; } = 1;
}

public class LootRareBonus
{
    public double Chance { get; set; }
    public List<LootPoolEntry> Pool { get; set; } = new();
}

public class LootTable
{
    public string Id { get; set; } = "";
    public List<LootGuaranteed> Guaranteed { get; set; } = new();
    public List<LootPoolEntry> Pool { get; set; } = new();
    public int PoolRolls { get; set; }
    public LootRareBonus? RareBonus { get; set; }
}
