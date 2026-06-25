using System.Collections.Generic;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Engine.Tests;

public class ItemTemplateSideCarTests
{
    [Fact]
    public void WriteItemTemplate_registers_into_live_registry_same_session()
    {
        using var root = new TempAuthoringRoot();
        root.RegisterBaseItem("tapestry-oracle:weapon-melee", "a weapon", "item");

        var id = root.WriteItemTemplate(
            areaId: "castle-kitchen",
            id: "castle-kitchen:loot-dented-ladle-0",
            baseId: "tapestry-oracle:weapon-melee",
            name: "Dented Ladle",
            desc: "A battered serving ladle.",
            properties: new() { ["damage_dice"] = "2d6", ["rarity"] = "common" });

        Assert.Equal("castle-kitchen:loot-dented-ladle-0", id);
        Assert.True(root.Items.HasTemplate("castle-kitchen:loot-dented-ladle-0"));
        var entity = root.Items.CreateItem("castle-kitchen:loot-dented-ladle-0");
        Assert.NotNull(entity);
        Assert.Equal("Dented Ladle", entity!.Name);
    }

    [Fact]
    public void WriteItemTemplate_round_trips_through_LoadItem()
    {
        using var root = new TempAuthoringRoot();
        root.RegisterBaseItem("tapestry-oracle:weapon-melee", "a weapon", "item");

        var id = root.WriteItemTemplate(
            areaId: "castle-kitchen",
            id: "castle-kitchen:loot-cleaver-1",
            baseId: "tapestry-oracle:weapon-melee",
            name: "Boning Cleaver",
            desc: "Keen and well-oiled.",
            properties: new() { ["damage_dice"] = "3d8", ["rarity"] = "uncommon" });

        var path = root.ItemSideCarPath("castle-kitchen", id!).Replace('\\', '/');
        Assert.True(System.IO.File.Exists(path));
        Assert.DoesNotContain("/areas/areas/", path);
        Assert.Contains("/castle-kitchen/items/", path);

        var reloaded = YamlContentLoader.LoadItem(System.IO.File.ReadAllText(path));
        Assert.Equal("castle-kitchen:loot-cleaver-1", reloaded.Id);
        Assert.Equal("Boning Cleaver", reloaded.Name);
        Assert.Equal("3d8", reloaded.Properties["damage_dice"].ToString());
    }

    [Fact]
    public void WriteItemTemplate_returns_null_for_unknown_base()
    {
        using var root = new TempAuthoringRoot();
        var id = root.WriteItemTemplate(
            areaId: "castle-kitchen",
            id: "castle-kitchen:loot-x-0",
            baseId: "tapestry-oracle:does-not-exist",
            name: "X", desc: "Y", properties: new());
        Assert.Null(id);
    }

    // BLOCKER 1: armor ac MUST land as Dictionary<string,int> or combat reads it as null (0 AC).
    [Fact]
    public void WriteItemTemplate_armor_ac_is_a_typed_int_map_live()
    {
        using var root = new TempAuthoringRoot();
        root.RegisterBaseItem("tapestry-oracle:armor-body", "a piece of armor", "item");

        var id = root.WriteItemTemplate(
            areaId: "castle-kitchen",
            id: "castle-kitchen:loot-plate-0",
            baseId: "tapestry-oracle:armor-body",
            name: "Dented Breastplate",
            desc: "Heavy and battered.",
            // The pack passes the rolled ac as a nested numeric map; the binding must coerce it.
            properties: new()
            {
                ["slot"] = "body",
                ["rarity"] = "common",
                ["ac"] = new Dictionary<string, object?> { ["slash"] = 3, ["pierce"] = 3, ["bash"] = 3, ["exotic"] = 3 },
            });

        Assert.NotNull(id);
        var entity = root.Items.CreateItem(id!);
        var ac = entity!.GetProperty<Dictionary<string, int>>("ac");
        Assert.NotNull(ac);              // null here == the silent-zero bug
        Assert.Equal(3, ac!["slash"]);
    }
}
