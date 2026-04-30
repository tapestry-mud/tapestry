using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ClassesModule : IJintApiModule
{
    private readonly ClassRegistry _registry;
    private readonly RaceRegistry _raceRegistry;
    private readonly World _world;
    private readonly ProficiencyManager _proficiency;

    public ClassesModule(ClassRegistry registry, RaceRegistry raceRegistry, World world, ProficiencyManager proficiency)
    {
        _registry = registry;
        _raceRegistry = raceRegistry;
        _world = world;
        _proficiency = proficiency;
    }

    public string Namespace => "classes";

    public object Build(JintEngine engine)
    {
        return new
        {
            register = new Action<JsValue>(definition =>
            {
                var obj = (ObjectInstance)definition;
                var id = obj.Get("id").ToString();
                var name = obj.Get("name").ToString();

                var priorityVal = obj.Get("priority");
                var priority = priorityVal.Type == Types.Number ? (int)(double)priorityVal.ToObject()! : 0;

                var packNameVal = engine.GetValue("__currentPack");
                var packName = (packNameVal.Type != Types.Undefined && packNameVal.Type != Types.Null)
                    ? packNameVal.ToString() : "";

                var statGrowth = new Dictionary<StatType, string>();
                var growthVal = obj.Get("stat_growth");
                if (growthVal.Type != Types.Undefined && growthVal.Type != Types.Null && growthVal is ObjectInstance growthObj)
                {
                    foreach (var prop in growthObj.GetOwnProperties())
                    {
                        var key = prop.Key.ToString().Replace("_", "");
                        if (Enum.TryParse<StatType>(key, true, out var stat))
                        {
                            statGrowth[stat] = prop.Value.Value!.ToString();
                        }
                    }
                }

                var taglineVal = obj.Get("tagline");
                var tagline = taglineVal.Type != Types.Undefined && taglineVal.Type != Types.Null
                    ? taglineVal.ToString() : "";

                var descriptionVal = obj.Get("description");
                var description = descriptionVal.Type != Types.Undefined && descriptionVal.Type != Types.Null
                    ? descriptionVal.ToString() : "";

                var trackVal = obj.Get("track");
                var track = trackVal.Type != Types.Undefined && trackVal.Type != Types.Null
                    ? trackVal.ToString() : "";

                var startingAlignmentVal = obj.Get("starting_alignment");
                var startingAlignment = startingAlignmentVal.Type == Types.Number
                    ? (int)(double)startingAlignmentVal.ToObject()! : 0;

                var levelUpFlavorVal = obj.Get("level_up_flavor");
                var levelUpFlavor = levelUpFlavorVal.Type != Types.Undefined && levelUpFlavorVal.Type != Types.Null
                    ? levelUpFlavorVal.ToString() : "";

                var allowedCategories = ParseStringArray(obj.Get("allowed_categories"));
                var allowedGenders = ParseStringArray(obj.Get("allowed_genders"));

                var path = new List<ClassPathEntry>();
                var pathVal = obj.Get("path");
                if (pathVal is JsArray pathArr)
                {
                    for (uint pi = 0; pi < pathArr.Length; pi++)
                    {
                        if (pathArr[(int)pi] is not ObjectInstance entryObj) { continue; }
                        var levelVal = entryObj.Get("level");
                        var abilityIdVal = entryObj.Get("ability_id");
                        var unlockedViaVal = entryObj.Get("unlocked_via");
                        if (levelVal.Type != Types.Number || abilityIdVal.Type == Types.Undefined) { continue; }
                        var entryLevel = (int)(double)levelVal.ToObject()!;
                        var entryAbilityId = abilityIdVal.ToString();
                        var entryUnlockedVia = (unlockedViaVal.Type != Types.Undefined && unlockedViaVal.Type != Types.Null)
                            ? unlockedViaVal.ToString() : null;
                        path.Add(new ClassPathEntry(entryLevel, entryAbilityId, entryUnlockedVia));
                    }
                }

                var trainsPerLevelVal = obj.Get("trains_per_level");
                var trainsPerLevel = trainsPerLevelVal.Type == Types.Number
                    ? (int)(double)trainsPerLevelVal.ToObject()!
                    : 5;

                var growthBonuses = new Dictionary<StatType, StatType>();
                var growthBonusesVal = obj.Get("growth_bonuses");
                if (growthBonusesVal.Type != Types.Undefined && growthBonusesVal.Type != Types.Null
                    && growthBonusesVal is ObjectInstance gbObj)
                {
                    foreach (var prop in gbObj.GetOwnProperties())
                    {
                        var vitalKey = prop.Key.ToString().Replace("_", "");
                        var attrVal = prop.Value.Value?.ToString().Replace("_", "") ?? "";
                        if (Enum.TryParse<StatType>(vitalKey, true, out var vital)
                            && Enum.TryParse<StatType>(attrVal, true, out var attr))
                        {
                            growthBonuses[vital] = attr;
                        }
                    }
                }

                _registry.Register(new ClassDefinition
                {
                    Id = id,
                    Name = name,
                    StatGrowth = statGrowth,
                    PackName = packName,
                    Priority = priority,
                    Tagline = tagline,
                    Description = description,
                    Track = track,
                    StartingAlignment = startingAlignment,
                    LevelUpFlavor = levelUpFlavor,
                    AllowedCategories = allowedCategories,
                    AllowedGenders = allowedGenders,
                    Path = path,
                    TrainsPerLevel = trainsPerLevel,
                    GrowthBonuses = growthBonuses
                });
            }),

            get = new Func<string, object?>(id =>
            {
                var def = _registry.Get(id);
                if (def == null) { return null; }
                var growth = new Dictionary<string, object?>();
                foreach (var kv in def.StatGrowth)
                {
                    growth[kv.Key.ToString().ToLower()] = kv.Value;
                }
                var path = def.Path.Select(e => (object)new
                {
                    level = e.Level,
                    ability_id = e.AbilityId,
                    unlocked_via = e.UnlockedVia
                }).ToArray();
                return new
                {
                    id = def.Id,
                    name = def.Name,
                    tagline = def.Tagline,
                    description = def.Description,
                    track = def.Track,
                    starting_alignment = def.StartingAlignment,
                    level_up_flavor = def.LevelUpFlavor,
                    allowed_categories = def.AllowedCategories.ToArray(),
                    allowed_genders = def.AllowedGenders.ToArray(),
                    stat_growth = growth,
                    pack_name = def.PackName,
                    path
                };
            }),

            getAll = new Func<object[]>(() =>
            {
                return _registry.GetAll()
                    .Select(d => (object)new { id = d.Id, name = d.Name })
                    .ToArray();
            }),

            getEligibleClasses = new Func<JsValue, object[]>(filterObj =>
            {
                var obj = (ObjectInstance)filterObj;
                var raceId = obj.Get("race").ToString();
                var gender = obj.Get("gender").ToString();

                var raceDef = _raceRegistry.Get(raceId);
                var category = (raceDef != null && !string.IsNullOrEmpty(raceDef.RaceCategory))
                    ? raceDef.RaceCategory
                    : raceId;

                return _registry.GetEligibleClasses(category, gender)
                    .Select(d => (object)new
                    {
                        id = d.Id,
                        name = d.Name,
                        tagline = d.Tagline,
                        description = d.Description
                    })
                    .ToArray();
            }),

            setClass = new Action<string, string>((entityIdStr, classId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }
                var def = _registry.Get(classId);
                if (def == null) { return; }
                var entity = _world.GetEntity(entityId);
                if (entity == null) { return; }

                entity.SetProperty("class", classId);

                var levelKey = ProgressionProperties.Level(def.Track);
                var level = entity.GetProperty<int>(levelKey);
                if (level < 1) { level = 1; }

                foreach (var entry in def.Path)
                {
                    if (entry.Level <= level)
                    {
                        _proficiency.Learn(entityId, entry.AbilityId, 1);
                    }
                }
            })
        };
    }

    private static List<string> ParseStringArray(JsValue val)
    {
        var list = new List<string>();
        if (val is not JsArray arr) { return list; }
        for (uint i = 0; i < arr.Length; i++)
        {
            var el = arr[(int)i];
            if (el.Type != Types.Undefined && el.Type != Types.Null)
            {
                list.Add(el.ToString());
            }
        }
        return list;
    }
}
