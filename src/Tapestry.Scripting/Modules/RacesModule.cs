using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class RacesModule : IJintApiModule
{
    private readonly RaceRegistry _registry;
    private readonly World _world;

    public RacesModule(RaceRegistry registry, World world)
    {
        _registry = registry;
        _world = world;
    }

    public string Namespace => "races";

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

                var castModVal = obj.Get("cast_cost_modifier");
                var castMod = castModVal.Type == Types.Number ? (int)(double)castModVal.ToObject()! : 0;

                var packNameVal = engine.GetValue("__currentPack");
                var packName = (packNameVal.Type != Types.Undefined && packNameVal.Type != Types.Null)
                    ? packNameVal.ToString() : "";

                var caps = new Dictionary<StatType, int>();
                var capsVal = obj.Get("stat_caps");
                if (capsVal.Type != Types.Undefined && capsVal.Type != Types.Null && capsVal is ObjectInstance capsObj)
                {
                    foreach (var prop in capsObj.GetOwnProperties())
                    {
                        var key = prop.Key.ToString().Replace("_", "");
                        if (Enum.TryParse<StatType>(key, true, out var stat)
                            && prop.Value.Value!.Type == Types.Number)
                        {
                            caps[stat] = (int)(double)prop.Value.Value.ToObject()!;
                        }
                    }
                }

                var flags = new List<string>();
                var flagsVal = obj.Get("racial_flags");
                if (flagsVal is JsArray flagArr)
                {
                    for (uint i = 0; i < flagArr.Length; i++)
                    {
                        var el = flagArr[i];
                        if (el.Type != Types.Undefined && el.Type != Types.Null)
                        {
                            flags.Add(el.ToString());
                        }
                    }
                }

                var taglineVal = obj.Get("tagline");
                var tagline = taglineVal.Type != Types.Undefined && taglineVal.Type != Types.Null
                    ? taglineVal.ToString() : "";

                var descriptionVal = obj.Get("description");
                var description = descriptionVal.Type != Types.Undefined && descriptionVal.Type != Types.Null
                    ? descriptionVal.ToString() : "";

                var raceCategoryVal = obj.Get("race_category");
                var raceCategory = raceCategoryVal.Type != Types.Undefined && raceCategoryVal.Type != Types.Null
                    ? raceCategoryVal.ToString() : "";

                var startingAlignmentVal = obj.Get("starting_alignment");
                var startingAlignment = startingAlignmentVal.Type == Types.Number
                    ? (int)(double)startingAlignmentVal.ToObject()! : 0;

                _registry.Register(new RaceDefinition
                {
                    Id = id,
                    Name = name,
                    StatCaps = caps,
                    CastCostModifier = castMod,
                    RacialFlags = flags,
                    PackName = packName,
                    Priority = priority,
                    Tagline = tagline,
                    Description = description,
                    RaceCategory = raceCategory,
                    StartingAlignment = startingAlignment
                });
            }),

            get = new Func<string, object?>(id =>
            {
                var def = _registry.Get(id);
                if (def == null) { return null; }
                var caps = new Dictionary<string, object?>();
                foreach (var kv in def.StatCaps)
                {
                    caps[kv.Key.ToString().ToLower()] = kv.Value;
                }
                return new
                {
                    id = def.Id,
                    name = def.Name,
                    tagline = def.Tagline,
                    description = def.Description,
                    race_category = def.RaceCategory,
                    starting_alignment = def.StartingAlignment,
                    stat_caps = caps,
                    cast_cost_modifier = def.CastCostModifier,
                    racial_flags = def.RacialFlags.ToArray(),
                    pack_name = def.PackName
                };
            }),

            getAll = new Func<object[]>(() =>
            {
                return _registry.GetAll()
                    .Select(d => (object)new
                    {
                        id = d.Id,
                        name = d.Name,
                        tagline = d.Tagline,
                        description = d.Description
                    })
                    .ToArray();
            }),

            getStatCap = new Func<string, string, int>((raceId, statName) =>
            {
                var def = _registry.Get(raceId);
                if (def == null) { return 25; }
                var key = statName.Replace("_", "");
                return Enum.TryParse<StatType>(key, true, out var stat)
                    ? def.StatCaps.GetValueOrDefault(stat, 25)
                    : 25;
            })
        };
    }
}
