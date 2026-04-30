// tests/Tapestry.Engine.Tests/Mobs/LootTableTests.cs
using Tapestry.Engine.Mobs;

namespace Tapestry.Engine.Tests.Mobs;

public class LootTableTests
{
    [Fact]
    public void Resolve_GuaranteedItems_AlwaysReturned()
    {
        var table = new LootTable
        {
            Id = "core:test-table",
            Guaranteed = new List<LootGuaranteed>
            {
                new() { Item = "core:goblin-ear", Count = 2 },
                new() { Item = "core:bone", Count = 1 }
            },
            Pool = new List<LootPoolEntry>(),
            PoolRolls = 0,
            RareBonus = null
        };

        var resolver = new LootTableResolver(new Random(42));
        var results = resolver.Resolve(table);

        Assert.Equal(3, results.Count);
        Assert.Equal(2, results.Count(r => r == "core:goblin-ear"));
        Assert.Equal(1, results.Count(r => r == "core:bone"));
    }

    [Fact]
    public void Resolve_Pool_ReturnsCorrectNumberOfRolls()
    {
        var table = new LootTable
        {
            Id = "core:test-table",
            Guaranteed = new List<LootGuaranteed>(),
            Pool = new List<LootPoolEntry>
            {
                new() { Item = "core:sword", Weight = 50 },
                new() { Item = "core:shield", Weight = 50 }
            },
            PoolRolls = 3,
            RareBonus = null
        };

        var resolver = new LootTableResolver(new Random(42));
        var results = resolver.Resolve(table);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r == "core:sword" || r == "core:shield"));
    }

    [Fact]
    public void Resolve_PoolWeighting_FavorsHigherWeight()
    {
        var table = new LootTable
        {
            Id = "core:test-table",
            Guaranteed = new List<LootGuaranteed>(),
            Pool = new List<LootPoolEntry>
            {
                new() { Item = "core:common", Weight = 90 },
                new() { Item = "core:rare", Weight = 10 }
            },
            PoolRolls = 1000,
            RareBonus = null
        };

        var resolver = new LootTableResolver(new Random(42));
        var results = resolver.Resolve(table);

        var commonCount = results.Count(r => r == "core:common");
        var rareCount = results.Count(r => r == "core:rare");

        Assert.True(commonCount > rareCount * 5,
            $"Common ({commonCount}) should heavily outweigh rare ({rareCount})");
    }

    [Fact]
    public void Resolve_RareBonus_RollsWhenChanceHit()
    {
        var table = new LootTable
        {
            Id = "core:test-table",
            Guaranteed = new List<LootGuaranteed>(),
            Pool = new List<LootPoolEntry>(),
            PoolRolls = 0,
            RareBonus = new LootRareBonus
            {
                Chance = 1.0,
                Pool = new List<LootPoolEntry>
                {
                    new() { Item = "core:rare-gem", Weight = 100 }
                }
            }
        };

        var resolver = new LootTableResolver(new Random(42));
        var results = resolver.Resolve(table);

        Assert.Single(results);
        Assert.Equal("core:rare-gem", results[0]);
    }

    [Fact]
    public void Resolve_RareBonus_SkippedWhenChanceMissed()
    {
        var table = new LootTable
        {
            Id = "core:test-table",
            Guaranteed = new List<LootGuaranteed>(),
            Pool = new List<LootPoolEntry>(),
            PoolRolls = 0,
            RareBonus = new LootRareBonus
            {
                Chance = 0.0,
                Pool = new List<LootPoolEntry>
                {
                    new() { Item = "core:rare-gem", Weight = 100 }
                }
            }
        };

        var resolver = new LootTableResolver(new Random(42));
        var results = resolver.Resolve(table);

        Assert.Empty(results);
    }

    [Fact]
    public void Resolve_AllTiersCombined()
    {
        var table = new LootTable
        {
            Id = "core:test-table",
            Guaranteed = new List<LootGuaranteed>
            {
                new() { Item = "core:ear", Count = 1 }
            },
            Pool = new List<LootPoolEntry>
            {
                new() { Item = "core:dagger", Weight = 100 }
            },
            PoolRolls = 1,
            RareBonus = new LootRareBonus
            {
                Chance = 1.0,
                Pool = new List<LootPoolEntry>
                {
                    new() { Item = "core:gem", Weight = 100 }
                }
            }
        };

        var resolver = new LootTableResolver(new Random(42));
        var results = resolver.Resolve(table);

        Assert.Equal(3, results.Count);
        Assert.Contains("core:ear", results);
        Assert.Contains("core:dagger", results);
        Assert.Contains("core:gem", results);
    }
}
