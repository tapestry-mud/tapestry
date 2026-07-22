using Tapestry.Engine;
using Tapestry.Engine.Items;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

public class RegistryRemovalTests
{
    [Fact]
    public void AreaRegistry_Unregister_RemovesEntry_AndIsIdempotent()
    {
        var registry = new AreaRegistry();
        registry.Register(new AreaDefinition { Id = "oracle-run-3f2a", Name = "the Ashen Hollow" });

        Assert.True(registry.Unregister("oracle-run-3f2a"));
        Assert.False(registry.Contains("oracle-run-3f2a"));
        Assert.False(registry.Unregister("oracle-run-3f2a"));
    }

    [Fact]
    public void OracleTableRegistry_RemoveByArea_ScopesOnColonPrefix()
    {
        var registry = new OracleTableRegistry();
        registry.Register(new OracleTable { Id = "oracle-run-3f2a:mobs", Kind = "mobs" });
        registry.Register(new OracleTable { Id = "oracle-run-3f2a:items", Kind = "items" });
        // Sibling area whose slug is a string-prefix of the target. Must survive.
        registry.Register(new OracleTable { Id = "oracle-run-3f2ab:mobs", Kind = "mobs" });

        var removed = registry.RemoveByArea("oracle-run-3f2a");

        Assert.Equal(2, removed);
        Assert.False(registry.Contains("oracle-run-3f2a:mobs"));
        Assert.False(registry.Contains("oracle-run-3f2a:items"));
        Assert.True(registry.Contains("oracle-run-3f2ab:mobs"));
    }

    [Fact]
    public void OracleTableRegistry_RemoveByArea_OnUnknownArea_RemovesNothing()
    {
        var registry = new OracleTableRegistry();
        registry.Register(new OracleTable { Id = "oracle-run-3f2a:mobs", Kind = "mobs" });

        Assert.Equal(0, registry.RemoveByArea("no-such-area"));
        Assert.True(registry.Contains("oracle-run-3f2a:mobs"));
    }

    [Fact]
    public void ItemRegistry_RemoveByArea_IsCaseSensitive_AndScopesOnColonPrefix()
    {
        var registry = new ItemRegistry();
        registry.Register(new ItemTemplate { Id = "oracle-run-3f2a:kit-p1-wield", Name = "a blade" });
        registry.Register(new ItemTemplate { Id = "oracle-run-3f2a:loot-01", Name = "a ring" });
        registry.Register(new ItemTemplate { Id = "oracle-run-3f2ab:loot-01", Name = "a sibling ring" });
        // The dictionary is case-sensitive; a differently-cased id is a DIFFERENT template.
        registry.Register(new ItemTemplate { Id = "ORACLE-RUN-3F2A:loot-02", Name = "a shouted ring" });

        var removed = registry.RemoveByArea("oracle-run-3f2a");

        Assert.Equal(2, removed);
        Assert.False(registry.HasTemplate("oracle-run-3f2a:kit-p1-wield"));
        Assert.False(registry.HasTemplate("oracle-run-3f2a:loot-01"));
        Assert.True(registry.HasTemplate("oracle-run-3f2ab:loot-01"));
        Assert.True(registry.HasTemplate("ORACLE-RUN-3F2A:loot-02"));
    }
}
