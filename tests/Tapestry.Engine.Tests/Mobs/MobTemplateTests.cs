using Tapestry.Engine.Mobs;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Tests.Mobs;

public class MobTemplateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var template = new MobTemplate
        {
            Id = "core:goblin",
            Name = "a goblin",
            Type = "npc",
            Tags = new List<string> { "npc", "mob", "hostile", "killable" },
            Behavior = "wander",
            Stats = new MobTemplateStats
            {
                Strength = 8,
                Dexterity = 12,
                Constitution = 8,
                Intelligence = 4,
                Wisdom = 4,
                Luck = 6,
                MaxHp = 40,
                MaxResource = 0,
                MaxMovement = 80
            },
            Properties = new Dictionary<string, object?>
            {
                ["level"] = 3,
                ["regen_hp"] = 1,
                ["wander_interval"] = 30,
                ["wander_boundary"] = "area",
                ["corpse_decay"] = 300,
                ["description"] = "A small goblin."
            },
            Equipment = new List<string> { "core:rusty-dagger" },
            LootTable = "core:goblin-common"
        };

        Assert.Equal("core:goblin", template.Id);
        Assert.Equal("a goblin", template.Name);
        Assert.Equal("npc", template.Type);
        Assert.Contains("hostile", template.Tags);
        Assert.Equal("wander", template.Behavior);
        Assert.Equal(8, template.Stats.Strength);
        Assert.Equal(40, template.Stats.MaxHp);
        Assert.Equal(3, template.Properties["level"]);
        Assert.Single(template.Equipment);
        Assert.Equal("core:goblin-common", template.LootTable);
    }

    [Fact]
    public void CreateEntity_BuildsEntityFromTemplate()
    {
        var template = new MobTemplate
        {
            Id = "core:goblin",
            Name = "a goblin",
            Type = "npc",
            Tags = new List<string> { "npc", "mob", "hostile" },
            Behavior = "wander",
            Stats = new MobTemplateStats
            {
                Strength = 8,
                Dexterity = 12,
                Constitution = 8,
                Intelligence = 4,
                Wisdom = 4,
                Luck = 6,
                MaxHp = 40,
                MaxResource = 0,
                MaxMovement = 80
            },
            Properties = new Dictionary<string, object?>
            {
                ["level"] = 3,
                ["regen_hp"] = 1,
                ["corpse_decay"] = 300
            },
            Equipment = new List<string>(),
            LootTable = null
        };

        var entity = template.CreateEntity();

        Assert.Equal("npc", entity.Type);
        Assert.Equal("a goblin", entity.Name);
        Assert.True(entity.HasTag("npc"));
        Assert.True(entity.HasTag("mob"));
        Assert.True(entity.HasTag("hostile"));
        Assert.Equal(8, entity.Stats.BaseStrength);
        Assert.Equal(12, entity.Stats.BaseDexterity);
        Assert.Equal(40, entity.Stats.BaseMaxHp);
        Assert.Equal(40, entity.Stats.Hp);
        Assert.Equal(80, entity.Stats.MaxMovement);
        Assert.Equal(3, entity.GetProperty<object>("level"));
        Assert.Equal("core:goblin", entity.GetProperty<string>("template_id"));
        Assert.Equal("wander", entity.GetProperty<string>("behavior"));
    }
}
