using Tapestry.Engine;
using Tapestry.Engine.Stats;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class StatsModule : IJintApiModule
{
    private readonly ApiStats _stats;
    private readonly StatDisplayNames _statDisplayNames;
    private readonly World _world;

    public StatsModule(ApiStats stats, StatDisplayNames statDisplayNames, World world)
    {
        _stats = stats;
        _statDisplayNames = statDisplayNames;
        _world = world;
    }

    public string Namespace => "stats";

    public object Build(JintEngine engine)
    {
        return new
        {
            setDisplayName = new Action<string, string>((stat, name) =>
            {
                _statDisplayNames.SetDisplayName(stat, name);
            }),
            getDisplayName = new Func<string, string>(_stats.GetStatDisplayName),
            restoreVitals = new Action<string>(_stats.RestoreVitals),
            addVital = new Action<string, string, int>(_stats.AddVital),
            addBaseAttribute = new Func<string, string, int, bool>(_stats.AddBaseAttribute),
            get = new Func<string, object?>(_stats.GetEntityStats),
            setBase = new Action<string, string, int>((entityIdStr, statName, value) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return; }
                var entity = _world.GetEntity(id);
                if (entity == null) { return; }
                var key = statName.Replace("_", "");
                if (!Enum.TryParse<StatType>(key, true, out var stat)) { return; }
                switch (stat)
                {
                    case StatType.Strength: entity.Stats.BaseStrength = value; break;
                    case StatType.Intelligence: entity.Stats.BaseIntelligence = value; break;
                    case StatType.Wisdom: entity.Stats.BaseWisdom = value; break;
                    case StatType.Dexterity: entity.Stats.BaseDexterity = value; break;
                    case StatType.Constitution: entity.Stats.BaseConstitution = value; break;
                    case StatType.Luck: entity.Stats.BaseLuck = value; break;
                }
                entity.Stats.Invalidate();
            })
        };
    }
}
