using Tapestry.Engine.Classes;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;
using Tapestry.Shared;
using System.Linq;

namespace Tapestry.Engine.Mobs;

public class SpawnManager
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly LootTableResolver _lootResolver;
    private readonly ItemRegistry _itemRegistry;
    private readonly ClassRegistry _classes;
    private readonly RaceRegistry _races;
    private readonly Random _random;
    private readonly Dictionary<string, MobTemplate> _templates = new();
    private readonly Dictionary<string, LootTable> _lootTables = new();
    private readonly Dictionary<string, AreaSpawnConfig> _areaConfigs = new();
    private readonly Dictionary<Guid, (string area, int spawnIndex)> _spawnTracking = new();

    public SpawnManager(World world, EventBus eventBus, LootTableResolver lootResolver,
                        ItemRegistry itemRegistry, ClassRegistry? classes = null, RaceRegistry? races = null,
                        Random? random = null)
    {
        _world = world;
        _eventBus = eventBus;
        _lootResolver = lootResolver;
        _itemRegistry = itemRegistry;
        _classes = classes ?? new ClassRegistry();
        _races = races ?? new RaceRegistry();
        _random = random ?? Random.Shared;

        _eventBus.Subscribe("area.tick", OnAreaTick);
    }

    private void OnAreaTick(GameEvent evt)
    {
        var areaId = evt.Data?.GetValueOrDefault("areaId") as string;
        if (areaId != null)
        {
            RunAreaReset(areaId);
        }
    }

    public void RegisterTemplate(MobTemplate template)
    {
        _templates[template.Id] = template;
    }

    public MobTemplate? GetTemplate(string id)
    {
        return _templates.GetValueOrDefault(id);
    }

    public void RegisterLootTable(LootTable table)
    {
        _lootTables[table.Id] = table;
    }

    public LootTable? GetLootTable(string id)
    {
        return _lootTables.GetValueOrDefault(id);
    }

    public void RegisterAreaSpawns(AreaSpawnConfig config)
    {
        _areaConfigs[config.Area] = config;
    }

    public void RegisterRoomSpawns(
        string areaId,
        string roomId,
        IEnumerable<(string Mob, int Count, RareSpawnConfig? Rare, IEnumerable<string> Tags)> rules,
        int effectiveResetInterval)
    {
        if (!_areaConfigs.TryGetValue(areaId, out var config))
        {
            config = new AreaSpawnConfig { Area = areaId, ResetInterval = effectiveResetInterval };
            _areaConfigs[areaId] = config;
        }

        foreach (var rule in rules)
        {
            config.Spawns.Add(new SpawnRule
            {
                Room = roomId,
                Mob = rule.Mob,
                Count = rule.Count,
                Rare = rule.Rare,
                Tags = rule.Tags.ToList()
            });
        }
    }

    public Entity? SpawnMob(string templateId, string roomId)
    {
        if (!_templates.TryGetValue(templateId, out var template))
        {
            return null;
        }

        var room = _world.GetRoom(roomId);
        if (room == null)
        {
            return null;
        }

        var entity = template.CreateEntity();
        MobStatDerivation.Apply(entity, template, _classes, _races);
        entity.LocationRoomId = roomId;
        room.AddEntity(entity);
        _world.TrackEntity(entity);

        // Instantiate and equip items from template
        foreach (var equipTemplateId in template.Equipment)
        {
            var item = _itemRegistry.CreateItem(equipTemplateId);
            if (item == null)
            {
                continue;
            }

            var slot = item.GetProperty<string>(InventoryProperties.Slot);
            if (slot != null)
            {
                entity.SetEquipment(slot, item);
            }

            // Apply stat modifiers from equipment
            var modifiers = item.GetProperty<List<StatModifier>>(InventoryProperties.Modifiers);
            if (modifiers != null)
            {
                foreach (var mod in modifiers)
                {
                    entity.Stats.AddModifier(new StatModifier(
                        $"equipment:{item.Id}", mod.Stat, mod.Value));
                }
            }

            _world.TrackEntity(item);
        }

        if (template.LootTable != null && _lootTables.TryGetValue(template.LootTable, out var lootTable))
        {
            var lootItemIds = _lootResolver.Resolve(lootTable);

            foreach (var lootItemId in lootItemIds)
            {
                var lootItem = _itemRegistry.CreateItem(lootItemId);
                if (lootItem != null)
                {
                    entity.AddToContents(lootItem);
                    _world.TrackEntity(lootItem);
                }
            }

            _eventBus.Publish(new GameEvent
            {
                Type = "mob.loot.generated",
                SourceEntityId = entity.Id,
                RoomId = roomId,
                Data = new Dictionary<string, object?>
                {
                    [CommonProperties.TemplateId] = templateId,
                    ["loot_count"] = lootItemIds.Count
                }
            });
        }

        _eventBus.Publish(new GameEvent
        {
            Type = "mob.spawn",
            SourceEntityId = entity.Id,
            RoomId = roomId,
            Data = new Dictionary<string, object?>
            {
                [CommonProperties.TemplateId] = templateId
            }
        });

        return entity;
    }

    public void RunAreaReset(string areaName)
    {
        if (!_areaConfigs.TryGetValue(areaName, out var config))
        {
            return;
        }

        // Purge tracking entries for mobs that have since died/been removed
        var deadKeys = _spawnTracking
            .Where(kvp => kvp.Value.area == areaName && _world.GetEntity(kvp.Key) == null)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in deadKeys)
        {
            _spawnTracking.Remove(key);
        }

        for (int i = 0; i < config.Spawns.Count; i++)
        {
            var rule = config.Spawns[i];
            var room = _world.GetRoom(rule.Room);
            if (room == null)
            {
                continue;
            }

            var isPersistent = rule.Tags.Contains("persistent");

            // Count all living mobs tracked under this spawn rule, regardless of current room
            var livingCount = _spawnTracking.Count(kvp => kvp.Value == (areaName, i));

            if (isPersistent && livingCount >= rule.Count)
            {
                continue;
            }

            var missing = rule.Count - livingCount;
            for (int j = 0; j < missing; j++)
            {
                var mobId = rule.Mob;

                if (rule.Rare != null && _random.NextDouble() < rule.Rare.Chance)
                {
                    mobId = rule.Rare.Mob;
                }

                var entity = SpawnMob(mobId, rule.Room);
                if (entity != null)
                {
                    _spawnTracking[entity.Id] = (areaName, i);
                }
            }
        }
    }

    public IEnumerable<string> GetAreaNames() => _areaConfigs.Keys;

    public AreaSpawnConfig? GetAreaConfig(string areaName) =>
        _areaConfigs.GetValueOrDefault(areaName);
}
