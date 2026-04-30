using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class AreaModule : IJintApiModule
{
    private readonly AreaTickService _areaTick;
    private readonly AreaRegistry _areaRegistry;

    public string Namespace => "area";

    public AreaModule(AreaTickService areaTick, AreaRegistry areaRegistry)
    {
        _areaTick = areaTick;
        _areaRegistry = areaRegistry;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            get = new Func<string, object?>(areaId =>
            {
                var def = _areaRegistry.Get(areaId);
                if (def == null) { return null; }
                return new
                {
                    id = def.Id,
                    name = def.Name,
                    levelRange = def.LevelRange,
                    resetInterval = def.ResetInterval,
                    occupiedModifier = def.OccupiedModifier,
                    weatherZone = def.WeatherZone,
                    flags = def.Flags.ToArray()
                };
            }),
            playerCount = new Func<string, int>(areaId => _areaTick.GetPlayerCount(areaId)),
            setResetInterval = new Action<string, int>((areaId, ticks) => _areaTick.SetResetInterval(areaId, ticks)),
            setOccupiedModifier = new Action<string, float>((areaId, mod) => _areaTick.SetOccupiedModifier(areaId, mod)),
            list = new Func<string[]>(() => _areaRegistry.All().Select(a => a.Id).ToArray())
        };
    }
}
