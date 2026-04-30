using FluentAssertions;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Mobs;

public class SpawnManagerTests
{
    private World CreateWorldWithRoom(string roomId)
    {
        var world = new World();
        var room = new Room(roomId, "Test Room", "A test room.");
        world.AddRoom(room);
        return world;
    }

    private MobTemplate CreateGoblinTemplate()
    {
        return new MobTemplate
        {
            Id = "core:goblin",
            Name = "a goblin",
            Type = "npc",
            Tags = new List<string> { "npc", "mob" },
            Behavior = "stationary",
            Stats = new MobTemplateStats { MaxHp = 40 },
            Properties = new Dictionary<string, object?>(),
            Equipment = new List<string>(),
            LootTable = null
        };
    }

    [Fact]
    public void RegisterTemplate_StoresTemplate()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var manager = new SpawnManager(world, eventBus, lootResolver, new ItemRegistry());

        var template = CreateGoblinTemplate();
        manager.RegisterTemplate(template);

        Assert.NotNull(manager.GetTemplate("core:goblin"));
    }

    [Fact]
    public void SpawnMob_CreatesEntityInRoom()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var manager = new SpawnManager(world, eventBus, lootResolver, new ItemRegistry());
        var template = CreateGoblinTemplate();
        manager.RegisterTemplate(template);

        var entity = manager.SpawnMob("core:goblin", "core:test-room");

        Assert.NotNull(entity);
        Assert.Equal("a goblin", entity.Name);
        Assert.Equal("core:test-room", entity.LocationRoomId);
        Assert.True(entity.HasTag("npc"));
        Assert.True(entity.HasTag("mob"));
        var room = world.GetRoom("core:test-room");
        Assert.Contains(entity, room!.Entities);
    }

    [Fact]
    public void SpawnMob_PublishesSpawnEvent()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var manager = new SpawnManager(world, eventBus, lootResolver, new ItemRegistry());
        var template = CreateGoblinTemplate();
        manager.RegisterTemplate(template);

        GameEvent? captured = null;
        eventBus.Subscribe("mob.spawn", evt => { captured = evt; });

        manager.SpawnMob("core:goblin", "core:test-room");

        Assert.NotNull(captured);
        Assert.Equal("mob.spawn", captured!.Type);
        Assert.Equal("core:test-room", captured.RoomId);
    }

    [Fact]
    public void SpawnMob_ResolvesLootTable()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver(new Random(42));
        var itemRegistry = new ItemRegistry();
        itemRegistry.Register(new ItemTemplate
        {
            Id = "core:ear",
            Name = "a goblin ear",
            Type = "item:junk",
            Tags = new List<string> { "item", "junk" },
            Properties = new Dictionary<string, object?> { ["weight"] = 0 },
            Modifiers = new List<ItemTemplate.ModifierEntry>()
        });
        var manager = new SpawnManager(world, eventBus, lootResolver, itemRegistry);

        var template = CreateGoblinTemplate();
        template.LootTable = "core:test-loot";
        manager.RegisterTemplate(template);

        var lootTable = new LootTable
        {
            Id = "core:test-loot",
            Guaranteed = new List<LootGuaranteed>
            {
                new() { Item = "core:ear", Count = 1 }
            },
            Pool = new List<LootPoolEntry>(),
            PoolRolls = 0,
            RareBonus = null
        };
        manager.RegisterLootTable(lootTable);

        var entity = manager.SpawnMob("core:goblin", "core:test-room");

        Assert.NotNull(entity);
        Assert.Single(entity!.Contents);
        Assert.Equal("a goblin ear", entity.Contents[0].Name);
    }

    [Fact]
    public void RunAreaReset_SpawnsMissingMobs()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var manager = new SpawnManager(world, eventBus, lootResolver, new ItemRegistry());
        var template = CreateGoblinTemplate();
        manager.RegisterTemplate(template);

        var areaConfig = new AreaSpawnConfig
        {
            Area = "test-area",
            ResetInterval = 300,
            Spawns = new List<SpawnRule>
            {
                new()
                {
                    Room = "core:test-room",
                    Mob = "core:goblin",
                    Count = 2
                }
            }
        };
        manager.RegisterAreaSpawns(areaConfig);

        manager.RunAreaReset("test-area");

        var room = world.GetRoom("core:test-room")!;
        var mobs = room.Entities.Where(e => e.Type == "npc").ToList();
        Assert.Equal(2, mobs.Count);
    }

    [Fact]
    public void RunAreaReset_SkipsPersistentLivingMobs()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var manager = new SpawnManager(world, eventBus, lootResolver, new ItemRegistry());
        var template = CreateGoblinTemplate();
        template.Id = "core:vendor";
        template.Name = "a vendor";
        manager.RegisterTemplate(template);

        var areaConfig = new AreaSpawnConfig
        {
            Area = "test-area",
            ResetInterval = 300,
            Spawns = new List<SpawnRule>
            {
                new()
                {
                    Room = "core:test-room",
                    Mob = "core:vendor",
                    Count = 1,
                    Tags = new List<string> { "persistent" }
                }
            }
        };
        manager.RegisterAreaSpawns(areaConfig);

        manager.RunAreaReset("test-area");
        var room = world.GetRoom("core:test-room")!;
        Assert.Single(room.Entities.Where(e => e.Type == "npc"));

        manager.RunAreaReset("test-area");
        Assert.Single(room.Entities.Where(e => e.Type == "npc"));
    }

    [Fact]
    public void RunAreaReset_OnlySpawnsMissingCount()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var manager = new SpawnManager(world, eventBus, lootResolver, new ItemRegistry());
        var template = CreateGoblinTemplate();
        manager.RegisterTemplate(template);

        var areaConfig = new AreaSpawnConfig
        {
            Area = "test-area",
            ResetInterval = 300,
            Spawns = new List<SpawnRule>
            {
                new()
                {
                    Room = "core:test-room",
                    Mob = "core:goblin",
                    Count = 3
                }
            }
        };
        manager.RegisterAreaSpawns(areaConfig);

        manager.RunAreaReset("test-area");
        var room = world.GetRoom("core:test-room")!;
        Assert.Equal(3, room.Entities.Count(e => e.Type == "npc"));

        var mob = room.Entities.First(e => e.Type == "npc");
        room.RemoveEntity(mob);
        world.UntrackEntity(mob);

        manager.RunAreaReset("test-area");
        Assert.Equal(3, room.Entities.Count(e => e.Type == "npc"));
    }

    private ItemRegistry CreateRegistryWithDagger()
    {
        var registry = new ItemRegistry();
        registry.Register(new ItemTemplate
        {
            Id = "core:rusty-dagger",
            Name = "a rusty dagger",
            Type = "item:weapon",
            Tags = new List<string> { "item", "weapon" },
            Properties = new Dictionary<string, object?> { ["slot"] = "wield", ["weight"] = 2 },
            Modifiers = new List<ItemTemplate.ModifierEntry>
            {
                new() { Stat = "dexterity", Value = 1 }
            }
        });
        return registry;
    }

    [Fact]
    public void SpawnMob_RareVariant_WhenChanceHit()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var manager = new SpawnManager(world, eventBus, lootResolver, new ItemRegistry(), random: new Random(42));

        var normalTemplate = CreateGoblinTemplate();
        manager.RegisterTemplate(normalTemplate);

        var rareTemplate = new MobTemplate
        {
            Id = "core:goblin-chief",
            Name = "the goblin chief",
            Type = "npc",
            Tags = new List<string> { "npc", "mob", "rare" },
            Behavior = "stationary",
            Stats = new MobTemplateStats { MaxHp = 120 },
            Properties = new Dictionary<string, object?>(),
            Equipment = new List<string>(),
            LootTable = null
        };
        manager.RegisterTemplate(rareTemplate);

        var areaConfig = new AreaSpawnConfig
        {
            Area = "test-area",
            ResetInterval = 300,
            Spawns = new List<SpawnRule>
            {
                new()
                {
                    Room = "core:test-room",
                    Mob = "core:goblin",
                    Count = 100,
                    Rare = new RareSpawnConfig
                    {
                        Mob = "core:goblin-chief",
                        Chance = 0.5
                    }
                }
            }
        };
        manager.RegisterAreaSpawns(areaConfig);
        manager.RunAreaReset("test-area");

        var room = world.GetRoom("core:test-room")!;
        var chiefs = room.Entities.Count(e => e.Name == "the goblin chief");
        var goblins = room.Entities.Count(e => e.Name == "a goblin");

        Assert.True(chiefs > 0, "Should have spawned at least one rare mob");
        Assert.True(goblins > 0, "Should have spawned at least one normal mob");
    }

    [Fact]
    public void SpawnMob_EquipsItemsFromTemplate()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var itemRegistry = CreateRegistryWithDagger();
        var manager = new SpawnManager(world, eventBus, lootResolver, itemRegistry);

        var template = new MobTemplate
        {
            Id = "core:goblin",
            Name = "a goblin",
            Type = "npc",
            Tags = new List<string> { "npc", "mob" },
            Stats = new MobTemplateStats { Dexterity = 12, MaxHp = 40 },
            Properties = new Dictionary<string, object?>(),
            Equipment = new List<string> { "core:rusty-dagger" },
            LootTable = null
        };
        manager.RegisterTemplate(template);

        var mob = manager.SpawnMob("core:goblin", "core:test-room");

        mob.Should().NotBeNull();
        var weapon = mob!.GetEquipment("wield");
        weapon.Should().NotBeNull();
        weapon!.Name.Should().Be("a rusty dagger");
        weapon.GetProperty<string>("template_id").Should().Be("core:rusty-dagger");
    }

    [Fact]
    public void SpawnMob_EquipmentModifiesMobStats()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var itemRegistry = CreateRegistryWithDagger();
        var manager = new SpawnManager(world, eventBus, lootResolver, itemRegistry);

        var template = new MobTemplate
        {
            Id = "core:goblin",
            Name = "a goblin",
            Type = "npc",
            Tags = new List<string> { "npc", "mob" },
            Stats = new MobTemplateStats { Dexterity = 12, MaxHp = 40 },
            Properties = new Dictionary<string, object?>(),
            Equipment = new List<string> { "core:rusty-dagger" },
            LootTable = null
        };
        manager.RegisterTemplate(template);

        var mob = manager.SpawnMob("core:goblin", "core:test-room");

        // Base 12 + dagger +1 = 13
        mob!.Stats.Dexterity.Should().Be(13);
    }

    [Fact]
    public void SpawnMob_EquipmentTrackedInWorld()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var itemRegistry = CreateRegistryWithDagger();
        var manager = new SpawnManager(world, eventBus, lootResolver, itemRegistry);

        var template = new MobTemplate
        {
            Id = "core:goblin",
            Name = "a goblin",
            Type = "npc",
            Tags = new List<string> { "npc", "mob" },
            Stats = new MobTemplateStats { MaxHp = 40 },
            Properties = new Dictionary<string, object?>(),
            Equipment = new List<string> { "core:rusty-dagger" },
            LootTable = null
        };
        manager.RegisterTemplate(template);

        var mob = manager.SpawnMob("core:goblin", "core:test-room");
        var weapon = mob!.GetEquipment("wield");

        world.GetEntity(weapon!.Id).Should().NotBeNull();
    }

    [Fact]
    public void SpawnMob_SkipsUnknownEquipmentTemplate()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver();
        var itemRegistry = new ItemRegistry(); // empty
        var manager = new SpawnManager(world, eventBus, lootResolver, itemRegistry);

        var template = new MobTemplate
        {
            Id = "core:goblin",
            Name = "a goblin",
            Type = "npc",
            Tags = new List<string> { "npc", "mob" },
            Stats = new MobTemplateStats { MaxHp = 40 },
            Properties = new Dictionary<string, object?>(),
            Equipment = new List<string> { "core:nonexistent-sword" },
            LootTable = null
        };
        manager.RegisterTemplate(template);

        var mob = manager.SpawnMob("core:goblin", "core:test-room");

        mob.Should().NotBeNull();
        mob!.Equipment.Should().BeEmpty();
    }

    private ItemRegistry CreateRegistryWithLootItems()
    {
        var registry = new ItemRegistry();
        registry.Register(new ItemTemplate
        {
            Id = "core:goblin-ear",
            Name = "a goblin ear",
            Type = "item:junk",
            Tags = new List<string> { "item", "junk" },
            Properties = new Dictionary<string, object?> { ["weight"] = 0, ["value"] = 2 },
            Modifiers = new List<ItemTemplate.ModifierEntry>()
        });
        registry.Register(new ItemTemplate
        {
            Id = "core:small-coin-pouch",
            Name = "a small coin pouch",
            Type = "item:currency",
            Tags = new List<string> { "item", "currency" },
            Properties = new Dictionary<string, object?> { ["weight"] = 0, ["value"] = 10 },
            Modifiers = new List<ItemTemplate.ModifierEntry>()
        });
        return registry;
    }

    [Fact]
    public void SpawnMob_InstantiatesLootInInventory()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver(new Random(42));
        var itemRegistry = CreateRegistryWithLootItems();
        var manager = new SpawnManager(world, eventBus, lootResolver, itemRegistry);

        var template = CreateGoblinTemplate();
        template.LootTable = "core:goblin-common";
        manager.RegisterTemplate(template);

        var lootTable = new LootTable
        {
            Id = "core:goblin-common",
            Guaranteed = new List<LootGuaranteed>
            {
                new() { Item = "core:goblin-ear", Count = 1 }
            },
            Pool = new List<LootPoolEntry>(),
            PoolRolls = 0
        };
        manager.RegisterLootTable(lootTable);

        var mob = manager.SpawnMob("core:goblin", "core:test-room");

        mob.Should().NotBeNull();
        mob!.Contents.Should().HaveCount(1);
        mob.Contents[0].Name.Should().Be("a goblin ear");
    }

    [Fact]
    public void SpawnMob_LootItemsTrackedInWorld()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver(new Random(42));
        var itemRegistry = CreateRegistryWithLootItems();
        var manager = new SpawnManager(world, eventBus, lootResolver, itemRegistry);

        var template = CreateGoblinTemplate();
        template.LootTable = "core:goblin-common";
        manager.RegisterTemplate(template);

        var lootTable = new LootTable
        {
            Id = "core:goblin-common",
            Guaranteed = new List<LootGuaranteed>
            {
                new() { Item = "core:goblin-ear", Count = 1 }
            },
            Pool = new List<LootPoolEntry>(),
            PoolRolls = 0
        };
        manager.RegisterLootTable(lootTable);

        var mob = manager.SpawnMob("core:goblin", "core:test-room");

        mob.Should().NotBeNull();
        var lootItem = mob!.Contents[0];
        world.GetEntity(lootItem.Id).Should().NotBeNull();
    }

    [Fact]
    public void RegisterRoomSpawns_AggregatesIntoAreaConfig()
    {
        var world = new World();
        var eventBus = new EventBus();
        var resolver = new LootTableResolver();
        var itemRegistry = new ItemRegistry();
        var spawnManager = new SpawnManager(world, eventBus, resolver, itemRegistry);

        var rules = new (string Mob, int Count, RareSpawnConfig? Rare, IEnumerable<string> Tags)[]
        {
            ("core:goblin", 2, null, Array.Empty<string>())
        };

        spawnManager.RegisterRoomSpawns("starter-town", "core:training-grounds", rules, 300);

        var config = spawnManager.GetAreaConfig("starter-town");
        config.Should().NotBeNull();
        config!.Spawns.Should().HaveCount(1);
        config.Spawns[0].Room.Should().Be("core:training-grounds");
        config.Spawns[0].Mob.Should().Be("core:goblin");
        config.Spawns[0].Count.Should().Be(2);
    }

    [Fact]
    public void SpawnMob_NoLootItemsStringProperty()
    {
        var world = CreateWorldWithRoom("core:test-room");
        var eventBus = new EventBus();
        var lootResolver = new LootTableResolver(new Random(42));
        var itemRegistry = CreateRegistryWithLootItems();
        var manager = new SpawnManager(world, eventBus, lootResolver, itemRegistry);

        var template = CreateGoblinTemplate();
        template.LootTable = "core:goblin-common";
        manager.RegisterTemplate(template);

        var lootTable = new LootTable
        {
            Id = "core:goblin-common",
            Guaranteed = new List<LootGuaranteed>
            {
                new() { Item = "core:goblin-ear", Count = 1 }
            },
            Pool = new List<LootPoolEntry>(),
            PoolRolls = 0
        };
        manager.RegisterLootTable(lootTable);

        var mob = manager.SpawnMob("core:goblin", "core:test-room");

        mob.Should().NotBeNull();
        mob!.HasProperty("loot_items").Should().BeFalse();
    }
}
