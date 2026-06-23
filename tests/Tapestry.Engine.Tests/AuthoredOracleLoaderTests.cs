using Tapestry.Engine;
using Tapestry.Scripting.Authoring;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AuthoredOracleLoaderTests
{
    [Fact]
    public void Loads_oracle_tables_from_the_authoring_root_at_boot()
    {
        using var root = new TempAuthoringRoot();
        root.WriteRawFile("castle-kitchen/mobs/mob-oracle-table.yaml", """
        oracle_table:
          kind: mobs
          entries:
            - { w: 60, id: angry-cook, name: "Angry Cook", desc: "Red-faced.", balance_ref: mob }
        """);
        var registry = new OracleTableRegistry();
        var loader = new AuthoredOracleLoader(root.RootPath, registry);

        loader.Load();

        Assert.True(registry.Contains("castle-kitchen:mobs"));
        Assert.Equal("angry-cook", registry.Get("castle-kitchen:mobs")!.Entries[0].Id);
    }

    [Fact]
    public void Loads_places_oracle_from_area_root()
    {
        using var root = new TempAuthoringRoot();
        root.WriteRawFile("crystal-cavern/places-oracle.yaml", """
        oracle_table:
          kind: places
          entries:
            - { w: 100, id: mossy-ledge, name: "Mossy Ledge", desc: "A damp ledge." }
        """);
        var registry = new OracleTableRegistry();
        var loader = new AuthoredOracleLoader(root.RootPath, registry);

        loader.Load();

        Assert.True(registry.Contains("crystal-cavern:places"));
        Assert.Equal("mossy-ledge", registry.Get("crystal-cavern:places")!.Entries[0].Id);
    }

    [Fact]
    public void Skips_non_oracle_yaml_files()
    {
        using var root = new TempAuthoringRoot();
        root.WriteRawFile("castle-kitchen/area.yaml", """
        area:
          id: castle-kitchen
          name: Castle Kitchen
        """);
        var registry = new OracleTableRegistry();
        var loader = new AuthoredOracleLoader(root.RootPath, registry);

        loader.Load();

        Assert.Empty(registry.All());
    }

    [Fact]
    public void Returns_gracefully_when_root_does_not_exist()
    {
        var registry = new OracleTableRegistry();
        var loader = new AuthoredOracleLoader("/no/such/directory", registry);

        var ex = Record.Exception(() => loader.Load());

        Assert.Null(ex);
        Assert.Empty(registry.All());
    }

    [Fact]
    public void Loads_multiple_tables_across_areas()
    {
        using var root = new TempAuthoringRoot();
        root.WriteRawFile("area-a/mobs/mob-oracle-table.yaml", """
        oracle_table:
          kind: mobs
          entries:
            - { w: 50, id: goblin, name: "Goblin" }
        """);
        root.WriteRawFile("area-b/items/item-oracle-table.yaml", """
        oracle_table:
          kind: items
          entries:
            - { w: 50, id: torch, name: "Torch" }
        """);
        var registry = new OracleTableRegistry();
        var loader = new AuthoredOracleLoader(root.RootPath, registry);

        loader.Load();

        Assert.True(registry.Contains("area-a:mobs"));
        Assert.True(registry.Contains("area-b:items"));
    }
}
