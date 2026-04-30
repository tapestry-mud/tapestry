// src/Tapestry.Engine/Mobs/LootTableResolver.cs
namespace Tapestry.Engine.Mobs;

public class LootTableResolver
{
    private readonly Random _random;

    public LootTableResolver(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public List<string> Resolve(LootTable table)
    {
        var results = new List<string>();

        foreach (var guaranteed in table.Guaranteed)
        {
            for (int i = 0; i < guaranteed.Count; i++)
            {
                results.Add(guaranteed.Item);
            }
        }

        for (int i = 0; i < table.PoolRolls; i++)
        {
            var picked = RollWeightedPool(table.Pool);
            if (picked != null)
            {
                results.Add(picked);
            }
        }

        if (table.RareBonus != null && table.RareBonus.Pool.Count > 0)
        {
            if (_random.NextDouble() < table.RareBonus.Chance)
            {
                var picked = RollWeightedPool(table.RareBonus.Pool);
                if (picked != null)
                {
                    results.Add(picked);
                }
            }
        }

        return results;
    }

    private string? RollWeightedPool(List<LootPoolEntry> pool)
    {
        if (pool.Count == 0)
        {
            return null;
        }

        var totalWeight = pool.Sum(e => e.Weight);
        var roll = _random.Next(totalWeight);
        var cumulative = 0;

        foreach (var entry in pool)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
            {
                return entry.Item;
            }
        }

        return pool[^1].Item;
    }
}
