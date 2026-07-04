using Tapestry.Engine.Abilities;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Combat;
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
    private readonly ProficiencyManager? _proficiencyManager;
    private readonly Dictionary<string, MobTemplate> _templates = new();
    private readonly Dictionary<string, LootTable> _lootTables = new();
    private readonly Dictionary<string, AreaSpawnConfig> _areaConfigs = new();
    private readonly Dictionary<Guid, (string area, int spawnIndex)> _spawnTracking = new();
    private readonly Dictionary<string, List<(string RoomId, List<string> FixtureTemplateIds)>> _areaFixtures = new();

    public SpawnManager(World world, EventBus eventBus, LootTableResolver lootResolver,
                        ItemRegistry itemRegistry, ClassRegistry? classes = null, RaceRegistry? races = null,
                        Random? random = null, ProficiencyManager? proficiencyManager = null)
    {
        _world = world;
        _eventBus = eventBus;
        _lootResolver = lootResolver;
        _itemRegistry = itemRegistry;
        _classes = classes ?? new ClassRegistry();
        _races = races ?? new RaceRegistry();
        _random = random ?? Random.Shared;
        _proficiencyManager = proficiencyManager;

        _eventBus.Subscribe("area.tick", OnAreaTick);
    }

    private void OnAreaTick(GameEvent evt)
    {
        var areaId = evt.Data?.GetValueOrDefault("areaId") as string;
        if (areaId != null)
        {
            RunAreaReset(areaId);
            RestorePlacements(areaId);
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
        IEnumerable<(string Mob, int Count, RareSpawnConfig? Rare, IEnumerable<string> Tags, SpawnOverride? Override)> rules,
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
                Tags = rule.Tags.ToList(),
                Override = rule.Override
            });
        }
    }

    public void RegisterRoomFixtures(string areaId, string roomId, IEnumerable<string> fixtureTemplateIds)
    {
        if (!_areaFixtures.TryGetValue(areaId, out var rooms))
        {
            rooms = new List<(string, List<string>)>();
            _areaFixtures[areaId] = rooms;
        }

        rooms.Add((roomId, fixtureTemplateIds.ToList()));
    }

    public Entity? SpawnMob(string templateId, string roomId, SpawnOverride? over = null)
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

        if (over != null)
        {
            ApplyOverride(entity, over);
        }

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

        if (over?.NoReroll != true && template.LootTable != null && _lootTables.TryGetValue(template.LootTable, out var lootTable))
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

        if (_proficiencyManager != null && template.Abilities.Count > 0)
        {
            foreach (var abilityEntry in template.Abilities)
            {
                if (string.IsNullOrEmpty(abilityEntry.Id))
                {
                    continue;
                }

                var proficiency = abilityEntry.Proficiency ?? template.AbilityProficiency ?? 85;
                _proficiencyManager.Learn(entity.Id, abilityEntry.Id, proficiency);
            }
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

    private void ApplyOverride(Entity entity, SpawnOverride over)
    {
        if (!string.IsNullOrWhiteSpace(over.Name)) { entity.Name = over.Name!; }
        if (!string.IsNullOrWhiteSpace(over.Desc)) { entity.SetProperty(CommonProperties.Description, over.Desc); }
        if (over.MaxHp is int hp)
        {
            entity.Stats.BaseMaxHp = hp;
            entity.Stats.SetVital(VitalKind.Hp, entity.Stats.MaxHp);
        }
        if (!string.IsNullOrWhiteSpace(over.Damage)) { entity.SetProperty(CombatProperties.DamageDice, over.Damage); }
        if (over.FromType != null) { entity.SetProperty("oracle_from_type", over.FromType); }
        foreach (var itemId in over.Items)
        {
            var item = _itemRegistry.CreateItem(itemId);
            if (item != null)
            {
                entity.AddToContents(item);
                _world.TrackEntity(item);
            }
        }
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

                // A frozen override means "the same instance returns" - never rare-swap it.
                if (rule.Override == null && rule.Rare != null && _random.NextDouble() < rule.Rare.Chance)
                {
                    mobId = rule.Rare.Mob;
                }

                var entity = SpawnMob(mobId, rule.Room, rule.Override);
                if (entity != null)
                {
                    _spawnTracking[entity.Id] = (areaName, i);
                }
            }
        }
    }

    public void RestorePlacements(string areaName)
    {
        if (!_areaFixtures.TryGetValue(areaName, out var rooms))
        {
            return;
        }

        foreach (var (roomId, fixtureTemplateIds) in rooms)
        {
            var room = _world.GetRoom(roomId);
            if (room == null)
            {
                continue;
            }

            var authored = fixtureTemplateIds
                .Select(id => new ItemTemplate.ContentEntry
                {
                    TemplateId = id,
                    Count = 1,
                    Contents = _itemRegistry.GetTemplate(id)?.Contents
                        ?? new List<ItemTemplate.ContentEntry>()
                })
                .ToList();

            RestoreChildren(room.Entities, room.AddEntity, authored);
        }
    }

    private void RestoreInto(Entity container, List<ItemTemplate.ContentEntry> authoredChildren)
    {
        RestoreChildren(container.Contents, container.AddToContents, authoredChildren);
    }

    // Top up the children of a target (room or container) to the authored quantity.
    // Children are grouped by template_id; desiredCount = sum Count across entries of that
    // template; the FIRST entry's subtree is authoritative for the whole group (two same-
    // template entries with different nested contents collapse to the first — use two
    // templates to vary contents). Presence is counted, not boolean, so N-stacks seed and
    // partial loot tops back up without ever overfilling.
    private void RestoreChildren(
        IReadOnlyList<Entity> currentChildren,
        Action<Entity> addChild,
        List<ItemTemplate.ContentEntry> authoredChildren)
    {
        var groups = new List<(string TemplateId, int Count, List<ItemTemplate.ContentEntry> Subtree)>();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in authoredChildren)
        {
            var count = entry.Count < 1 ? 1 : entry.Count;
            if (index.TryGetValue(entry.TemplateId, out var gi))
            {
                var g = groups[gi];
                groups[gi] = (g.TemplateId, g.Count + count, g.Subtree);
            }
            else
            {
                index[entry.TemplateId] = groups.Count;
                groups.Add((entry.TemplateId, count, entry.Contents));
            }
        }

        foreach (var (templateId, desiredCount, subtree) in groups)
        {
            var present = currentChildren
                .Where(e => string.Equals(
                    e.GetProperty<string>(CommonProperties.TemplateId),
                    templateId,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            for (int i = present.Count; i < desiredCount; i++)
            {
                var instance = _itemRegistry.CreateItem(templateId);
                if (instance == null)
                {
                    break;
                }

                addChild(instance);
                _world.TrackEntity(instance);
                present.Add(instance);
            }

            foreach (var instance in present.Take(desiredCount))
            {
                RestoreInto(instance, subtree);
            }
        }
    }

    public IEnumerable<MobTemplate> AllTemplates => _templates.Values;

    public IEnumerable<string> GetAreaNames() => _areaConfigs.Keys;

    public AreaSpawnConfig? GetAreaConfig(string areaName) =>
        _areaConfigs.GetValueOrDefault(areaName);

    /// <summary>Returns the spawn rules for a specific room, for projecting into the room
    /// sidecar. Returns an empty sequence if the area or room has no registered rules.</summary>
    public IEnumerable<SpawnRule> GetRoomSpawns(string areaId, string roomId)
    {
        if (!_areaConfigs.TryGetValue(areaId, out var config))
        {
            return Enumerable.Empty<SpawnRule>();
        }
        return config.Spawns.Where(s => string.Equals(s.Room, roomId, StringComparison.OrdinalIgnoreCase));
    }
}
