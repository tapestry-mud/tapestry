using Microsoft.Extensions.Logging;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Persistence;

public class PlayerLoadResult
{
    public required Entity Entity { get; init; }
    public required Guid AccountId { get; init; }
    public required List<Entity> AllItems { get; init; }
}

public class PlayerSerializer
{
    private readonly PropertyRegistry _registry;
    private readonly ILogger<PlayerSerializer> _logger;

    public PlayerSerializer(PropertyRegistry registry, ILogger<PlayerSerializer> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public PlayerSaveData ToSaveData(
        Entity player,
        Guid accountId,
        List<Entity> allItems)
    {
        return new PlayerSaveData
        {
            Version = 1,
            Id = player.Id.ToString(),
            AccountId = accountId.ToString(),
            Name = player.Name,
            Type = player.Type,
            Location = player.LocationRoomId ?? "",
            Tags = player.Tags.ToList(),
            Roles = player.Roles.ToList(),
            Stats = SerializeStats(player.Stats),
            Properties = SerializeProperties(player, player.GetAllProperties()),
            Equipment = SerializeEquipment(player.Equipment),
            Inventory = player.Contents.Select(e => e.Id.ToString()).ToList(),
            Items = allItems.Select(i => SerializeItem(i, player)).ToList()
        };
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

        // Roles -- restore from saved roles field
        foreach (var role in data.Roles ?? new List<string>())
        {
            entity.AddRole(role);
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

            foreach (var keyword in itemData.Keywords ?? new List<string>())
            {
                item.AddKeyword(keyword);
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

        return new PlayerLoadResult
        {
            Entity = entity,
            AccountId = Guid.TryParse(data.AccountId, out var aid) ? aid : Guid.Empty,
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

    private Dictionary<string, object?> SerializeProperties(Entity owner, IReadOnlyDictionary<string, object?> properties)
    {
        var result = new Dictionary<string, object?>();

        foreach (var kvp in properties)
        {
            // Bare-name resolution so pack properties (stored "pack:name") are found without pack context.
            if (_registry.TryResolveByName(kvp.Key, out var entry))
            {
                if (entry.Transient) { continue; }

                result[kvp.Key] = kvp.Value switch
                {
                    Dictionary<string, int> mapInt =>
                        mapInt.ToDictionary(e => e.Key, e => (object?)e.Value),
                    Dictionary<string, string> mapStr =>
                        mapStr.ToDictionary(e => e.Key, e => (object?)e.Value),
                    List<string> listStr => listStr.ToList(),
                    // A JS-set list_string may arrive as a Jint array unwrapped to List<object>
                    // or object[] — materialize it as List<string> so no CLR type tag is emitted.
                    System.Collections.IEnumerable enumerable when entry.ValueType == PropertyValueType.ListString =>
                        enumerable.Cast<object?>().Select(v => v?.ToString() ?? "").ToList(),
                    _ => kvp.Value
                };
            }
            else
            {
                // Unknown key: DROP it (do not envelope). One structured warning per key so a
                // script setting an unregistered property surfaces in logs instead of persisting forever.
                _logger.LogWarning(
                    "Dropping unregistered property {Key} on {EntityType} {EntityId} ({EntityName}) - register it or move it to flow scratch",
                    kvp.Key, owner.Type, owner.Id, owner.Name);
            }
        }

        return result;
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
            Keywords = item.Keywords.ToList(),
            Properties = SerializeProperties(item, item.GetAllProperties())
        };
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
        stats.InitializeVitals(data.Vitals.Hp, data.Vitals.Resource, data.Vitals.Movement);
    }

    // Read-old-write-new property key migrations, applied as a save's property bag
    // materializes on load. A pair here rewrites a legacy persisted key to its current
    // name so existing player saves don't silently lose the value when an engine
    // property is renamed. Slice 2 introduces this with the wimpy_pct pair; Slice 7
    // extends it with the ROM negation trio -- add one tuple per rename, no other
    // change needed. no_follow is already snake_case and needs no entry.
    private static readonly (string OldKey, string NewKey)[] PropertyKeyMigrations =
    {
        ("wimpy_threshold", "wimpy_pct"),
        ("notell", "no_tell"),
        ("nochannels", "no_channels"),
        ("noemote", "no_emote"),
    };

    private void DeserializeProperties(Entity entity, Dictionary<string, object?> properties)
    {
        foreach (var (key, value) in properties)
        {
            var migratedKey = MigratePropertyKey(key, properties);
            if (migratedKey == null)
            {
                // Superseded: the save already carries the current key too, so the
                // legacy entry is dropped rather than overwriting it.
                continue;
            }
            var deserializedValue = DeserializePropertyValue(migratedKey, value);
            entity.SetProperty(migratedKey, deserializedValue);
        }
    }

    private static string? MigratePropertyKey(string key, Dictionary<string, object?> properties)
    {
        foreach (var (oldKey, newKey) in PropertyKeyMigrations)
        {
            if (key != oldKey)
            {
                continue;
            }
            return properties.ContainsKey(newKey) ? null : newKey;
        }
        return key;
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

        // Known property (engine or pack) — coerce to registered type.
        // Use TryResolveByName so pack properties stored as "{pack}:{name}" are found
        // without requiring a pack context.
        var valueType = _registry.TryResolveByName(key, out var resolvedEntry)
            ? (PropertyValueType?)resolvedEntry.ValueType
            : _registry.GetValueType(key);
        var registeredType = valueType.HasValue ? ValueTypeToClrType(valueType.Value) : null;
        if (registeredType != null)
        {
            try
            {
                return CoerceToRegisteredType(registeredType, value);
            }
            catch
            {
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

    private static Type? ValueTypeToClrType(PropertyValueType vt) => vt switch
    {
        PropertyValueType.String => typeof(string),
        PropertyValueType.Int => typeof(int),
        PropertyValueType.Double => typeof(double),
        PropertyValueType.Bool => typeof(bool),
        PropertyValueType.Long => typeof(long),
        PropertyValueType.MapInt => typeof(Dictionary<string, int>),
        PropertyValueType.MapString => typeof(Dictionary<string, string>),
        PropertyValueType.ListString => typeof(List<string>),
        _ => null
    };

    private static object? CoerceToRegisteredType(Type targetType, object? value)
    {
        if (value == null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType == typeof(Dictionary<string, int>))
        {
            if (value is Dictionary<string, object?> rawDict)
            {
                return rawDict.ToDictionary(
                    kv => kv.Key,
                    kv => Convert.ToInt32(kv.Value));
            }
            return new Dictionary<string, int>();
        }

        if (targetType == typeof(Dictionary<string, string>))
        {
            if (value is Dictionary<string, object?> rawDict)
            {
                return rawDict.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value?.ToString() ?? "");
            }
            return new Dictionary<string, string>();
        }

        if (targetType == typeof(List<string>))
        {
            if (value is List<object> rawList)
            {
                return rawList.Select(v => v?.ToString() ?? "").ToList();
            }
            if (value is List<string> strList)
            {
                return strList;
            }
            return new List<string>();
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        return Convert.ChangeType(value, targetType);
    }
}
