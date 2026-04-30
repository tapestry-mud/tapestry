using Tapestry.Engine;
using Tapestry.Engine.Mobs;
using Tapestry.Shared;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Scripting;

public record RoomLoadResult(
    Room Room,
    List<RoomSpawnModel> Spawns,
    List<string> Fixtures,
    int? ResetInterval);

public class RoomSpawnModel
{
    public string Mob { get; set; } = "";
    public int Count { get; set; } = 1;
    public RareSpawnConfig? Rare { get; set; }
    public List<string> Tags { get; set; } = new();
}

public static class YamlContentLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static PackManifest LoadManifest(string yaml)
    {
        return Deserializer.Deserialize<PackManifest>(yaml);
    }

    public static RoomLoadResult LoadRoom(string yaml)
    {
        var def = Deserializer.Deserialize<RoomDefinition>(yaml);
        var room = BuildRoom(def);
        return new RoomLoadResult(
            room,
            def.Spawns ?? new List<RoomSpawnModel>(),
            def.Fixtures ?? new List<string>(),
            def.ResetInterval);
    }

    public static ItemDefinition LoadItem(string yaml)
    {
        return Deserializer.Deserialize<ItemDefinition>(yaml);
    }

    public static (MobTemplate Template, LootTable? LootTable) LoadMob(string yaml)
    {
        var def = Deserializer.Deserialize<MobFileDefinition>(yaml);

        var template = new MobTemplate
        {
            Id = def.Id,
            Name = def.Name,
            Type = def.Type,
            Tags = def.Tags,
            Behavior = def.Behavior,
            Stats = def.Stats,
            Properties = def.Properties,
            Equipment = def.Equipment,
            Class = def.Class,
            Race = def.Race,
            Level = def.Level,
            Disposition = def.Disposition
        };

        LootTable? lootTable = null;
        if (def.Loot != null)
        {
            lootTable = new LootTable
            {
                Id = def.Id,
                Guaranteed = def.Loot.Guaranteed,
                Pool = def.Loot.Pool,
                PoolRolls = def.Loot.PoolRolls,
                RareBonus = def.Loot.RareBonus
            };
            template.LootTable = def.Id;
        }

        return (template, lootTable);
    }

    public static List<SlotDefinitionModel> LoadEquipmentSlots(string yaml)
    {
        var doc = Deserializer.Deserialize<SlotFileModel>(yaml);
        return doc.EquipmentSlots;
    }

    public static AreaDefinition LoadAreaDefinition(string yaml)
    {
        var doc = Deserializer.Deserialize<AreaFileModel>(yaml);
        var m = doc.Area;
        return new AreaDefinition
        {
            Id = m.Id,
            Name = m.Name,
            LevelRange = m.LevelRange ?? [1, 99],
            ResetInterval = m.ResetInterval,
            OccupiedModifier = m.OccupiedModifier,
            WeatherZone = m.WeatherZone,
            Flags = m.Flags ?? new(),
            WeatherMessages = m.WeatherMessages != null
                ? m.WeatherMessages.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new WeatherMessages(kvp.Value.Start, kvp.Value.Ongoing, kvp.Value.End))
                : new(),
            TimeMessages = m.TimeMessages ?? new()
        };
    }

    public static List<WeatherZoneDefinition> LoadWeatherZones(string yaml)
    {
        var doc = Deserializer.Deserialize<WeatherZonesFileModel>(yaml);
        var results = new List<WeatherZoneDefinition>();
        foreach (var (id, m) in doc.WeatherZones)
        {
            var terrainMessages = new Dictionary<string, Dictionary<string, WeatherMessages>>(StringComparer.OrdinalIgnoreCase);
            if (m.TerrainMessages != null)
            {
                foreach (var (terrain, stateMap) in m.TerrainMessages)
                {
                    var stateMessages = new Dictionary<string, WeatherMessages>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (state, msgModel) in stateMap)
                    {
                        stateMessages[state] = new WeatherMessages(msgModel.Start, msgModel.Ongoing, msgModel.End);
                    }
                    terrainMessages[terrain] = stateMessages;
                }
            }

            results.Add(new WeatherZoneDefinition
            {
                Id = id,
                States = m.States ?? new(),
                Transitions = m.Transitions ?? new(),
                TerrainMessages = terrainMessages,
                TerrainTransitions = m.TerrainTransitions ?? new()
            });
        }
        return results;
    }

    public static Dictionary<string, ThemeEntryModel> LoadTheme(string yaml)
    {
        var doc = Deserializer.Deserialize<ThemeFileModel>(yaml);
        return doc.Theme;
    }

    private static Exit ParseExit(object exitValue)
    {
        // String shorthand: "core:inn"
        if (exitValue is string targetStr)
        {
            return new Exit(targetStr);
        }

        // Object: { target: "core:inn", name: "...", door: { ... } }
        if (exitValue is Dictionary<object, object> dict)
        {
            var target = dict.TryGetValue("target", out var t) ? t?.ToString() ?? "" : "";
            var exit = new Exit(target);

            if (dict.TryGetValue("name", out var n) && n != null)
            {
                exit.DisplayName = n.ToString();
            }

            if (dict.TryGetValue("door", out var doorObj) && doorObj is Dictionary<object, object> doorDict)
            {
                exit.Door = ParseDoor(doorDict);
            }

            return exit;
        }

        return new Exit("");
    }

    private static DoorState ParseDoor(Dictionary<object, object> dict)
    {
        var name = dict.TryGetValue("name", out var n) ? n?.ToString() ?? "door" : "door";
        var isClosed = dict.TryGetValue("closed", out var c) && c != null && Convert.ToBoolean(c);
        var isLocked = dict.TryGetValue("locked", out var l) && l != null && Convert.ToBoolean(l);

        return new DoorState
        {
            Name = name,
            IsClosed = isClosed,
            IsLocked = isLocked,
            DefaultClosed = isClosed,
            DefaultLocked = isLocked,
            KeyId = dict.TryGetValue("key", out var k) ? k?.ToString() : null,
            IsPickable = dict.TryGetValue("pickable", out var p) && p != null && Convert.ToBoolean(p),
            PickDifficulty = dict.TryGetValue("pick_difficulty", out var pd) && pd != null
                ? Convert.ToInt32(pd) : 0
        };
    }

    public static void MirrorDoorsAcrossRooms(List<Room> rooms)
    {
        var roomMap = rooms.ToDictionary(r => r.Id);

        foreach (var room in rooms)
        {
            foreach (var direction in room.AvailableExits())
            {
                var exit = room.GetExit(direction)!;
                if (exit.Door == null) { continue; }
                if (!roomMap.TryGetValue(exit.TargetRoomId, out var targetRoom)) { continue; }

                var reverseExit = targetRoom.GetExit(direction.Opposite());
                if (reverseExit == null || reverseExit.Door != null) { continue; }

                reverseExit.Door = new DoorState
                {
                    Name = exit.Door.Name,
                    IsClosed = exit.Door.DefaultClosed,
                    IsLocked = exit.Door.DefaultLocked,
                    DefaultClosed = exit.Door.DefaultClosed,
                    DefaultLocked = exit.Door.DefaultLocked,
                    KeyId = exit.Door.KeyId,
                    IsPickable = exit.Door.IsPickable,
                    PickDifficulty = exit.Door.PickDifficulty
                };
            }
        }
    }

    private static Room BuildRoom(RoomDefinition def)
    {
        var room = new Room(def.Id, def.Name, def.Description);

        if (def.Exits != null)
        {
            foreach (var (dirStr, exitValue) in def.Exits)
            {
                if (DirectionExtensions.TryParse(dirStr, out var dir))
                {
                    room.SetExit(dir, ParseExit(exitValue));
                }
            }
        }

        if (def.KeywordExits != null)
        {
            foreach (var (keyword, exitValue) in def.KeywordExits)
            {
                room.SetKeywordExit(keyword, ParseExit(exitValue));
            }
        }

        if (def.Tags != null)
        {
            foreach (var tag in def.Tags)
            {
                room.AddTag(tag);
            }
        }

        if (def.Properties != null)
        {
            foreach (var (key, value) in def.Properties)
            {
                room.SetProperty(key, value);
            }
        }

        if (def.EntryPointDescription != null)
        {
            room.SetProperty("entry_point_description", def.EntryPointDescription);
        }
        if (def.EntryPointDirection != null)
        {
            room.SetProperty("entry_point_direction", def.EntryPointDirection);
        }

        if (def.AlignmentRange != null)
        {
            int? min = null, max = null;
            if (def.AlignmentRange.TryGetValue("min", out var minObj) && minObj != null)
            {
                min = Convert.ToInt32(minObj);
            }
            if (def.AlignmentRange.TryGetValue("max", out var maxObj) && maxObj != null)
            {
                max = Convert.ToInt32(maxObj);
            }
            room.AlignmentRange = new Tapestry.Engine.Alignment.AlignmentRange { Min = min, Max = max };
            room.AlignmentBlockMessage = def.AlignmentBlockMessage;
        }

        room.Area = def.Area;
        var terrain = def.Properties != null && def.Properties.TryGetValue("terrain", out var t) ? t?.ToString() ?? "" : "";
        var shelteredTerrain = terrain is "indoors" or "underground";
        room.WeatherExposed = def.WeatherExposed ?? !shelteredTerrain;
        room.TimeExposed = def.TimeExposed ?? !shelteredTerrain;

        if (def.WeatherMessages != null)
        {
            foreach (var (state, msgModel) in def.WeatherMessages)
            {
                room.WeatherMessages[state] = new WeatherMessages(msgModel.Start, msgModel.Ongoing, msgModel.End);
            }
        }

        if (def.TimeMessages != null)
        {
            foreach (var (period, msg) in def.TimeMessages)
            {
                room.TimeMessages[period] = msg;
            }
        }

        return room;
    }

    // Private model classes

    private class ThemeFileModel
    {
        public Dictionary<string, ThemeEntryModel> Theme { get; set; } = new();
    }

    public class ThemeEntryModel
    {
        public string? Fg { get; set; }
        public string? Bg { get; set; }
    }

    public class ItemDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        public List<ModifierDef> Modifiers { get; set; } = new();
    }

    public class ModifierDef
    {
        public string Stat { get; set; } = "";
        public int Value { get; set; }
    }

    private class SlotFileModel
    {
        public List<SlotDefinitionModel> EquipmentSlots { get; set; } = new();
    }

    public class SlotDefinitionModel
    {
        public string Name { get; set; } = "";
        public string Display { get; set; } = "";
        public int Max { get; set; } = 1;
    }

    private class RoomDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, object>? Exits { get; set; }
        public Dictionary<string, object>? KeywordExits { get; set; }
        public List<string>? Tags { get; set; }
        public Dictionary<string, object>? Properties { get; set; }
        public Dictionary<string, object?>? AlignmentRange { get; set; }
        public string? AlignmentBlockMessage { get; set; }
        public string? Area { get; set; }
        public bool? WeatherExposed { get; set; }
        public bool? TimeExposed { get; set; }
        public Dictionary<string, WeatherMessagesModel>? WeatherMessages { get; set; }
        public Dictionary<string, string>? TimeMessages { get; set; }
        public List<RoomSpawnModel>? Spawns { get; set; }
        public int? ResetInterval { get; set; }
        public List<string>? Fixtures { get; set; }
        public string? EntryPointDescription { get; set; }
        public string? EntryPointDirection { get; set; }
    }

    private class AreaFileModel
    {
        public AreaDefinitionModel Area { get; set; } = new();
    }

    private class AreaDefinitionModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int[]? LevelRange { get; set; }
        public int ResetInterval { get; set; } = 3000;
        public float OccupiedModifier { get; set; } = 3.0f;
        public string? WeatherZone { get; set; }
        public List<string>? Flags { get; set; }
        public Dictionary<string, WeatherMessagesModel>? WeatherMessages { get; set; }
        public Dictionary<string, string>? TimeMessages { get; set; }
    }

    private class WeatherZonesFileModel
    {
        public Dictionary<string, WeatherZoneModel> WeatherZones { get; set; } = new();
    }

    private class WeatherZoneModel
    {
        public List<string>? States { get; set; }
        public Dictionary<string, Dictionary<string, int>>? Transitions { get; set; }
        public Dictionary<string, Dictionary<string, WeatherMessagesModel>>? TerrainMessages { get; set; }
        public Dictionary<string, Dictionary<string, string>>? TerrainTransitions { get; set; }
    }

    private class WeatherMessagesModel
    {
        public string? Start { get; set; }
        public string? Ongoing { get; set; }
        public string? End { get; set; }
    }

    private class MobFileDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "npc";
        public List<string> Tags { get; set; } = new();
        public string Behavior { get; set; } = "stationary";
        public MobTemplateStats Stats { get; set; } = new();
        public Dictionary<string, object?> Properties { get; set; } = new();
        public List<string> Equipment { get; set; } = new();
        public string? Class { get; set; }
        public string? Race { get; set; }
        public int Level { get; set; }
        public DispositionModel? Disposition { get; set; }
        public LootInlineModel? Loot { get; set; }
    }

    private class LootInlineModel
    {
        public List<LootGuaranteed> Guaranteed { get; set; } = new();
        public List<LootPoolEntry> Pool { get; set; } = new();
        public int PoolRolls { get; set; }
        public LootRareBonus? RareBonus { get; set; }
    }
}
