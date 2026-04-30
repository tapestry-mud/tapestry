using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Progression;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ProgressionModule : IJintApiModule
{
    private readonly ProgressionManager _progression;
    private readonly ILogger<ProgressionModule> _logger;

    public ProgressionModule(ProgressionManager progression, ILogger<ProgressionModule> logger)
    {
        _progression = progression;
        _logger = logger;
    }

    public string Namespace => "progression";

    public object Build(JintEngine engine)
    {
        return new
        {
            registerTrack = new Action<JsValue>((definition) =>
            {
                var obj = (ObjectInstance)definition;
                var name = obj.Get("name").ToString();
                var maxLevel = (int)(double)obj.Get("max_level").ToObject()!;

                int[]? xpTable = null;
                var xpTableVal = obj.Get("xp_table");
                if (xpTableVal is JsArray tableArray)
                {
                    xpTable = new int[tableArray.Length];
                    for (uint i = 0; i < tableArray.Length; i++)
                    {
                        xpTable[i] = (int)(double)tableArray[i].ToObject()!;
                    }
                }

                Func<int, int>? xpFormula = null;
                var xpFormulaVal = obj.Get("xp_formula");
                if (xpFormulaVal.Type != Types.Undefined && xpFormulaVal.Type != Types.Null)
                {
                    xpFormula = (level) =>
                    {
                        var result = engine.Invoke(xpFormulaVal, null, new object[] { level });
                        return (int)(double)result.ToObject()!;
                    };
                }

                Action<Guid, string, int>? onLevelUp = null;
                var onLevelUpVal = obj.Get("on_level_up");
                if (onLevelUpVal.Type != Types.Undefined && onLevelUpVal.Type != Types.Null)
                {
                    onLevelUp = (entityId, trackName, newLevel) =>
                    {
                        engine.Invoke(onLevelUpVal, null, new object[] { entityId.ToString(), trackName, newLevel });
                    };
                }

                var deathPenaltyVal = obj.Get("death_penalty");
                var deathPenalty = 0.0;
                if (deathPenaltyVal.Type != Types.Undefined && deathPenaltyVal.Type != Types.Null)
                {
                    deathPenalty = (double)deathPenaltyVal.ToObject()!;
                }

                var track = new TrackDefinition
                {
                    Name = name,
                    MaxLevel = maxLevel,
                    XpTable = xpTable,
                    XpFormula = xpFormula,
                    OnLevelUp = onLevelUp,
                    DeathPenalty = deathPenalty
                };

                _progression.RegisterTrack(track);
                _logger.LogInformation("Registered progression track: {TrackName} (max level {MaxLevel})", name, maxLevel);
            }),

            grant = new Action<string, int, string, string>((entityIdStr, amount, trackName, source) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }
                _progression.GrantExperience(entityId, amount, trackName, source);
            }),

            deduct = new Action<string, int, string>((entityIdStr, amount, trackName) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }
                _progression.DeductExperience(entityId, amount, trackName);
            }),

            getInfo = new Func<string, string, object?>((entityIdStr, trackName) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return null;
                }
                var info = _progression.GetTrackInfo(entityId, trackName);
                if (info == null)
                {
                    return null;
                }
                return new
                {
                    xp = info.Xp,
                    level = info.Level,
                    xpToNext = info.XpToNext,
                    currentLevelThreshold = info.CurrentLevelThreshold,
                    maxLevel = info.MaxLevel,
                    overflow = info.Overflow
                };
            }),

            getLevel = new Func<string, string, int>((entityIdStr, trackName) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return 0;
                }
                return _progression.GetLevel(entityId, trackName);
            }),

            reset = new Action<string, string>((entityIdStr, trackName) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return;
                }
                _progression.ResetTrack(entityId, trackName);
            }),

            getTracks = new Func<object[]>(() =>
            {
                var tracks = _progression.GetAllTracks();
                var result = new object[tracks.Count];
                for (var i = 0; i < tracks.Count; i++)
                {
                    result[i] = new
                    {
                        name = tracks[i].Name,
                        max_level = tracks[i].MaxLevel,
                        death_penalty = tracks[i].DeathPenalty
                    };
                }
                return result;
            }),

            calculateMobXp = new Func<int, int, int, int>((killerLevel, mobLevel, baseXp) =>
            {
                var levelDiff = killerLevel - mobLevel;
                if (levelDiff <= 0)
                {
                    return baseXp;
                }
                if (levelDiff >= 10)
                {
                    return 0;
                }
                var scale = 1.0 - (levelDiff * 0.1);
                return Math.Max(1, (int)(baseXp * scale));
            }),

            groupShare = new Func<int, double>((combatantCount) =>
            {
                if (combatantCount <= 1)
                {
                    return 1.0;
                }
                return 1.0 / (combatantCount * 0.6 + 0.4);
            })
        };
    }
}
