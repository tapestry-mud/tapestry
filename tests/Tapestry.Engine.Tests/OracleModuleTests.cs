using Tapestry.Engine;
using Tapestry.Scripting.Modules;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class OracleModuleTests
{
    [Fact]
    public void table_returns_entries_for_known_id()
    {
        var reg = new OracleTableRegistry();
        reg.Register(new OracleTable
        {
            Id = "castle-kitchen:mobs",
            Kind = "mobs",
            Entries = new()
            {
                new OracleEntry { W = 60, Id = "angry-cook", Name = "Angry Cook", BalanceRef = "mob" },
            },
        });
        var module = new OracleModule(reg);

        var host = module.Build(null!);
        var tableFn = (Func<string, object?>)host.GetType().GetProperty("table")!.GetValue(host)!;
        var result = tableFn("castle-kitchen:mobs");

        Assert.NotNull(result);
    }

    [Fact]
    public void table_returns_null_for_unknown_id()
    {
        var module = new OracleModule(new OracleTableRegistry());
        var host = module.Build(null!);
        var tableFn = (Func<string, object?>)host.GetType().GetProperty("table")!.GetValue(host)!;
        Assert.Null(tableFn("nope"));
    }

    [Fact]
    public void table_maps_all_entry_fields()
    {
        var reg = new OracleTableRegistry();
        reg.Register(new OracleTable
        {
            Id = "area:items",
            Kind = "items",
            Entries = new()
            {
                new OracleEntry
                {
                    W = 40,
                    Id = "ladle",
                    Name = "Ladle",
                    Desc = "Dented.",
                    BalanceRef = "weapon",
                    Rarity = "common",
                    Extra = new Dictionary<string, string> { ["custom_field"] = "rare-drop" },
                },
            },
        });
        var module = new OracleModule(reg);

        var host = module.Build(null!);
        var tableFn = (Func<string, object?>)host.GetType().GetProperty("table")!.GetValue(host)!;
        var result = tableFn("area:items")!;

        var entries = (object[])result.GetType().GetProperty("entries")!.GetValue(result)!;
        Assert.Single(entries);

        var entry = entries[0];
        var entryType = entry.GetType();
        Assert.Equal(40, entryType.GetProperty("w")!.GetValue(entry));
        Assert.Equal("ladle", entryType.GetProperty("id")!.GetValue(entry));
        Assert.Equal("Ladle", entryType.GetProperty("name")!.GetValue(entry));
        Assert.Equal("Dented.", entryType.GetProperty("desc")!.GetValue(entry));
        Assert.Equal("weapon", entryType.GetProperty("balance_ref")!.GetValue(entry));
        Assert.Equal("common", entryType.GetProperty("rarity")!.GetValue(entry));
        var extra = (Dictionary<string, string>)entryType.GetProperty("extra")!.GetValue(entry)!;
        Assert.Equal("rare-drop", extra["custom_field"]);
    }

    [Fact]
    public void namespace_is_oracle()
    {
        var module = new OracleModule(new OracleTableRegistry());
        Assert.Equal("oracle", module.Namespace);
    }
}
