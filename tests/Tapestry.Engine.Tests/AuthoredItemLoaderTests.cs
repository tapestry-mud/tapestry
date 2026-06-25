using System.Collections.Generic;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Items;
using Tapestry.Engine.Persistence;
using Tapestry.Scripting.Authoring;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AuthoredItemLoaderTests
{
    [Fact]
    public void Loads_item_side_cars_from_the_authoring_root_at_boot()
    {
        using var root = new TempAuthoringRoot();
        root.WriteRawFile("castle-kitchen/items/loot-dented-ladle-0.yaml", """
        id: "castle-kitchen:loot-dented-ladle-0"
        name: "Dented Ladle"
        type: "item"
        keywords: [weapon, melee]
        tags: []
        properties:
          slot: wield
          damage_dice: "2d6"
          rarity: common
          description: "A battered serving ladle."
        """);
        var registry = new ItemRegistry();
        // PropertyRegistry is REQUIRED (BLOCKER 1). Register combat props so ac is MapInt.
        var props = new PropertyRegistry();
        CombatProperties.Register(props);
        var loader = new AuthoredItemLoader(root.Path, registry, props);

        loader.Load();

        Assert.True(registry.HasTemplate("castle-kitchen:loot-dented-ladle-0"));
        Assert.Equal("Dented Ladle", registry.CreateItem("castle-kitchen:loot-dented-ladle-0")!.Name);
    }

    // BLOCKER 1 reload half: armor ac must reload as Dictionary<string,int>, not the raw map.
    [Fact]
    public void Reloaded_armor_ac_is_a_typed_int_map()
    {
        using var root = new TempAuthoringRoot();
        root.WriteRawFile("castle-kitchen/items/loot-plate-0.yaml", """
        id: "castle-kitchen:loot-plate-0"
        name: "Dented Breastplate"
        type: "item"
        keywords: [armor, body]
        tags: []
        properties:
          slot: body
          rarity: common
          description: "Heavy and battered."
          ac:
            slash: 3
            pierce: 3
            bash: 3
            exotic: 3
        """);
        var registry = new ItemRegistry();
        var props = new PropertyRegistry();
        CombatProperties.Register(props);
        var loader = new AuthoredItemLoader(root.Path, registry, props);

        loader.Load();

        var ac = registry.CreateItem("castle-kitchen:loot-plate-0")!.GetProperty<Dictionary<string, int>>("ac");
        Assert.NotNull(ac);              // null here == the silent-zero bug on reload
        Assert.Equal(3, ac!["slash"]);
    }
}
