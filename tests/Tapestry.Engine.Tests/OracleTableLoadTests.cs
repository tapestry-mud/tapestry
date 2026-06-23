using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Engine.Tests;

public class OracleTableLoadTests
{
    [Fact]
    public void LoadOracleTable_parses_envelope_and_weighted_entries()
    {
        var yaml = """
        oracle_table:
          kind: mobs
          entries:
            - { w: 60, id: angry-cook, name: "Angry Cook", desc: "Red-faced.", balance_ref: mob }
            - { w: 40, id: scullion,   name: "Scullion",   desc: "Sooty.",     balance_ref: mob }
        """;

        var table = YamlContentLoader.LoadOracleTable(yaml);

        Assert.Equal("mobs", table.Kind);
        Assert.Equal(2, table.Entries.Count);
        Assert.Equal(60, table.Entries[0].W);
        Assert.Equal("angry-cook", table.Entries[0].Id);
        Assert.Equal("Angry Cook", table.Entries[0].Name);
        Assert.Equal("mob", table.Entries[0].BalanceRef);
    }

    [Fact]
    public void LoadOracleTable_captures_unknown_entry_keys_in_extra()
    {
        var yaml = """
        oracle_table:
          kind: items
          entries:
            - { w: 70, id: ladle, name: "Ladle", desc: "Dented.", balance_ref: weapon, rarity: common, custom_field: rare-drop }
        """;

        var table = YamlContentLoader.LoadOracleTable(yaml);

        Assert.Equal("common", table.Entries[0].Rarity);
        Assert.Equal("rare-drop", table.Entries[0].Extra["custom_field"]);
    }

    [Fact]
    public void SerializeOracleTable_round_trips_entries()
    {
        var table = new Tapestry.Shared.OracleTable
        {
            Kind = "mobs",
            Entries = new()
            {
                new Tapestry.Shared.OracleEntry
                {
                    W = 60,
                    Id = "angry-cook",
                    Name = "Angry Cook",
                    BalanceRef = "mob",
                },
            },
        };

        var yaml = YamlContentLoader.SerializeOracleTable(table);
        var reloaded = YamlContentLoader.LoadOracleTable(yaml);

        Assert.Equal("mobs", reloaded.Kind);
        Assert.Single(reloaded.Entries);
        Assert.Equal("angry-cook", reloaded.Entries[0].Id);
        Assert.Equal(60, reloaded.Entries[0].W);
    }

    [Fact]
    public void Registry_registers_and_gets_by_id()
    {
        var reg = new OracleTableRegistry();
        var table = new Tapestry.Shared.OracleTable
        {
            Id = "castle:kitchen:mobs",
            Kind = "mobs",
            Entries = new(),
        };

        reg.Register(table);

        Assert.True(reg.Contains("castle:kitchen:mobs"));
        Assert.Equal("mobs", reg.Get("castle:kitchen:mobs")!.Kind);
    }
}
