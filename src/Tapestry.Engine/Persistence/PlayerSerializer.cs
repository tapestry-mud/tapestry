using Tapestry.Engine.Inventory;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Persistence;

public class PlayerLoadResult
{
    public required Entity Entity { get; init; }
    public required string PasswordHash { get; init; }
    public required List<(Entity Corpse, List<Entity> Items)> Corpses { get; init; }
    public required List<Entity> AllItems { get; init; }
}

public class PlayerSerializer
{
    private readonly PropertyTypeRegistry _registry;

    public PlayerSerializer(PropertyTypeRegistry registry)
    {
        _registry = registry;
    }

    public PlayerSaveData ToSaveData(
        Entity player,
        string passwordHash,
        List<Entity> allItems,
        List<(Entity Corpse, List<Entity> Items)> corpses)
    {
        var dto = new PlayerSaveData
        {
            Version = 1,
            Id = player.Id.ToString(),
            Name = player.Name,
            Type = player.Type,
            Location = player.LocationRoomId ?? "",
            PasswordHash = passwordHash,
            Tags = player.Tags.ToList(),
            Stats = SerializeStats(player.Stats),
            Properties = SerializeProperties(player.GetAllProperties()),
            Equipment = SerializeEquipment(player.Equipment),
            Inventory = player.Contents.Select(e => e.Id.ToString()).ToList(),
            Items = allItems.Select(i => SerializeItem(i, player)).ToList(),
            Corpses = SerializeCorpses(corpses)
        };

        // Add corpse items to the flat item list
        foreach (var (_, items) in corpses)
        {
            foreach (var item in items)
            {
                dto.Items.Add(SerializeItem(item, null));
            }
        }

        return dto;
    }

    public PlayerLoadResult FromSaveData(PlayerSaveData data)
    {
        var id = Guid.Parse(data.Id);
        var entity = new Entity(data.Type, data.Name, id);
        entity.LocationRoomId = string.IsNullOrEmpty(data.Location) ? null : data.Location;

        // Tags
        foreach (var tag in data.Tags ?? new List<string>())
        {
            entity.AddTag(tag);
        }

        // Stats
        DeserializeStats(entity.Stats, data.Stats ?? new StatsSaveData());

        // Properties
        DeserializeProperties(entity, data.Properties ?? new Dictionary<string, object?>());

        // Rebuild items from flat list
        var itemDict = new Dictionary<Guid, Entity>();
        var containerRefs = new Dictionary<Guid, string>();
        var allItems = new List<Entity>();

        foreach (var itemData in data.Items ?? new List<ItemSaveData>())
        {
            var itemId = Guid.Parse(itemData.Id);
            var item = new Entity(itemData.Type, itemData.Name, itemId);

            foreach (var tag in itemData.Tags ?? new List<string>())
            {
                item.AddTag(tag);
            }

            DeserializeProperties(item, itemData.Properties ?? new Dictionary<string, object?>());

            itemDict[itemId] = item;
            allItems.Add(item);

            if (!string.IsNullOrEmpty(itemData.Container))
            {
                containerRefs[itemId] = itemData.Container;
            }
        }

        // Wire container relationships
        foreach (var (childId, containerIdStr) in containerRefs)
        {
            if (Guid.TryParse(containerIdStr, out var containerId) && itemDict.TryGetValue(containerId, out var container))
            {
                container.AddToContents(itemDict[childId]);
            }
        }

        // Wire inventory (top-level items into player contents)
        foreach (var itemIdStr in data.Inventory ?? new List<string>())
        {
            if (Guid.TryParse(itemIdStr, out var itemId) && itemDict.TryGetValue(itemId, out var item))
            {
                entity.AddToContents(item);
            }
        }

        // Wire equipment
        foreach (var (slot, itemIdStr) in data.Equipment ?? new Dictionary<string, string>())
        {
            if (Guid.TryParse(itemIdStr, out var itemId) && itemDict.TryGetValue(itemId, out var item))
            {
                entity.SetEquipment(slot, item);
            }
        }

        // Rebuild corpses
        var corpseList = new List<(Entity Corpse, List<Entity> Items)>();
        foreach (var corpseData in data.Corpses ?? new List<CorpseSaveData>())
        {
            var corpseId = Guid.Parse(corpseData.Id);
            var corpse = new Entity("corpse", corpseData.Name, corpseId);
            corpse.LocationRoomId = string.IsNullOrEmpty(corpseData.Location) ? null : corpseData.Location;

            foreach (var tag in corpseData.Tags ?? new List<string>())
            {
                corpse.AddTag(tag);
            }

            DeserializeProperties(corpse, corpseData.Properties ?? new Dictionary<string, object?>());

            var corpseItems = new List<Entity>();
            foreach (var contentIdStr in corpseData.Contents ?? new List<string>())
            {
                if (Guid.TryParse(contentIdStr, out var contentId) && itemDict.TryGetValue(contentId, out var item))
                {
                    corpse.AddToContents(item);
                    corpseItems.Add(item);
                }
            }

            corpseList.Add((corpse, corpseItems));
        }

        return new PlayerLoadResult
        {
            Entity = entity,
            PasswordHash = data.PasswordHash,
            Corpses = corpseList,
            AllItems = allItems
        };
    }

    // --- Serialization helpers ---

    private StatsSaveData SerializeStats(StatBlock stats)
    {
        return new StatsSaveData
        {
            Base = new BaseStatsSaveData
            {
                Strength = stats.BaseStrength,
                Intelligence = stats.BaseIntelligence,
                Wisdom = stats.BaseWisdom,
                Dexterity = stats.BaseDexterity,
                Constitution = stats.BaseConstitution,
                Luck = stats.BaseLuck,
                MaxHp = stats.BaseMaxHp,
                MaxResource = stats.BaseMaxResource,
                MaxMovement = stats.BaseMaxMovement
            },
            Vitals = new VitalsSaveData
            {
                Hp = stats.Hp,
                Resource = stats.Resource,
                Movement = stats.Movement
            },
            Modifiers = stats.Modifiers.Select(m => new ModifierSaveData
            {
                Source = m.Source,
                Stat = m.Stat.ToString(),
                Value = m.Value
            }).ToList()
        };
    }

    private Dictionary<string, object?> SerializeProperties(IReadOnlyDictionary<string, object?> properties)
    {
        var result = new Dictionary<string, object?>();

        foreach (var kvp in properties)
        {
            if (_registry.IsTransient(kvp.Key)) { continue; }

            if (_registry.IsRegistered(kvp.Key))
            {
                result[kvp.Key] = kvp.Value;
            }
            else
            {
                result[kvp.Key] = SerializeTaggedValue(kvp.Value);
            }
        }

        return result;
    }

    private Dictionary<string, object?> SerializeTaggedValue(object? value)
    {
        var typeName = value switch
        {
            int => "int",
            long => "long",
            float => "float",
            double => "double",
            bool => "bool",
            string => "string",
            _ => value?.GetType().Name ?? "null"
        };

        return new Dictionary<string, object?>
        {
            ["type"] = typeName,
            ["value"] = value
        };
    }

    private Dictionary<string, string> SerializeEquipment(IReadOnlyDictionary<string, Entity> equipment)
    {
        var result = new Dictionary<string, string>();
        foreach (var (slot, entity) in equipment)
        {
            result[slot] = entity.Id.ToString();
        }
        return result;
    }

    private ItemSaveData SerializeItem(Entity item, Entity? directParent)
    {
        string? containerId = null;
        if (item.Container != null && item.Container != directParent)
        {
            containerId = item.Container.Id.ToString();
        }

        return new ItemSaveData
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Type = item.Type,
            Container = containerId,
            Tags = item.Tags.ToList(),
            Properties = SerializeProperties(item.GetAllProperties())
        };
    }

    private List<CorpseSaveData> SerializeCorpses(List<(Entity Corpse, List<Entity> Items)> corpses)
    {
        return corpses.Select(c => new CorpseSaveData
        {
            Id = c.Corpse.Id.ToString(),
            Name = c.Corpse.Name,
            Location = c.Corpse.LocationRoomId ?? "",
            Tags = c.Corpse.Tags.ToList(),
            Properties = SerializeProperties(c.Corpse.GetAllProperties()),
            Contents = c.Items.Select(i => i.Id.ToString()).ToList()
        }).ToList();
    }

    // --- Deserialization helpers ---

    private void DeserializeStats(StatBlock stats, StatsSaveData data)
    {
        // Base stats
        stats.BaseStrength = data.Base.Strength;
        stats.BaseIntelligence = data.Base.Intelligence;
        stats.BaseWisdom = data.Base.Wisdom;
        stats.BaseDexterity = data.Base.Dexterity;
        stats.BaseConstitution = data.Base.Constitution;
        stats.BaseLuck = data.Base.Luck;
        stats.BaseMaxHp = data.Base.MaxHp;
        stats.BaseMaxResource = data.Base.MaxResource;
        stats.BaseMaxMovement = data.Base.MaxMovement;

        // Modifiers (must come before vitals so max values are correct)
        foreach (var mod in data.Modifiers ?? new List<ModifierSaveData>())
        {
            if (Enum.TryParse<StatType>(mod.Stat, out var statType))
            {
                stats.AddModifier(new StatModifier(mod.Source, statType, mod.Value));
            }
        }

        // Vitals (after base + modifiers so clamping works correctly)
        stats.Hp = data.Vitals.Hp;
        stats.Resource = data.Vitals.Resource;
        stats.Movement = data.Vitals.Movement;
    }

    private void DeserializeProperties(Entity entity, Dictionary<string, object?> properties)
    {
        foreach (var (key, value) in properties)
        {
            var deserializedValue = DeserializePropertyValue(key, value);
            entity.SetProperty(key, deserializedValue);
        }
    }

    private object? DeserializePropertyValue(string key, object? value)
    {
        // YamlDotNet returns Dictionary<object, object> for mappings — normalize to string keys first
        value = NormalizeYamlDict(value);

        // Check if it's a tagged dict (unknown property that was serialized with type info)
        if (value is Dictionary<string, object?> taggedDict
            && taggedDict.TryGetValue("type", out var typeObj)
            && taggedDict.TryGetValue("value", out var innerValue))
        {
            return CoerceToType(typeObj?.ToString(), innerValue);
        }

        // Known property — coerce to registered type
        var registeredType = _registry.GetType(key);
        if (registeredType != null)
        {
            try
            {
                return CoerceToRegisteredType(registeredType, value);
            }
            catch
            {
                // TODO: log warning with property key, expected type, and actual value (spec section 3.3)
                // Bad value for known property — return default
                return registeredType.IsValueType ? Activator.CreateInstance(registeredType) : null;
            }
        }

        // Unknown untagged — pass through
        return value;
    }

    // YamlDotNet deserializes mappings as Dictionary<object, object>; normalize to string-keyed recursively
    private static object? NormalizeYamlDict(object? value)
    {
        if (value is Dictionary<object, object> rawDict)
        {
            return rawDict.ToDictionary(
                kvp => kvp.Key?.ToString() ?? "",
                kvp => NormalizeYamlDict(kvp.Value));
        }

        return value;
    }

    private static object? CoerceToType(string? typeName, object? value)
    {
        if (value == null)
        {
            return null;
        }

        // Normalize in case inner value is also a raw YAML dict
        value = NormalizeYamlDict(value);

        // Recursively unwrap nested tagged dicts — self-heals saves that grew extra nesting layers
        while (value is Dictionary<string, object?> innerDict
               && innerDict.TryGetValue("type", out var innerType)
               && innerDict.TryGetValue("value", out var innerVal))
        {
            typeName = innerType?.ToString();
            value = NormalizeYamlDict(innerVal);
        }

        return typeName switch
        {
            "int" => Convert.ToInt32(value),
            "long" => Convert.ToInt64(value),
            "float" => Convert.ToSingle(value),
            "double" => Convert.ToDouble(value),
            "bool" => Convert.ToBoolean(value),
            "string" => value?.ToString(),
            _ => value
        };
    }

    private static object? CoerceToRegisteredType(Type targetType, object? value)
    {
        if (value == null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        return Convert.ChangeType(value, targetType);
    }
}
