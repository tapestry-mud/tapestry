using System.Text.RegularExpressions;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Races;
using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Mobs;

/// <summary>
/// Applies class stat growth (averaged per level) and race flags/property on mob spawn.
/// Called directly from SpawnManager after entity creation.
/// </summary>
public static class MobStatDerivation
{
    /// <summary>
    /// Applies class stat growth (averaged per level) and race flags/property to the entity.
    /// </summary>
    public static void Apply(Entity entity, MobTemplate template, ClassRegistry classes, RaceRegistry races)
    {
        if (!string.IsNullOrEmpty(template.Class) && template.Level > 0)
        {
            var classDef = classes.Get(template.Class);
            if (classDef != null)
            {
                foreach (var kv in classDef.StatGrowth)
                {
                    var avgPerLevel = AverageDice(kv.Value);
                    var total = avgPerLevel * template.Level;
                    AddToBase(entity, kv.Key, total);
                }
                entity.SetProperty("class", template.Class);
                entity.SetProperty("level", template.Level);
                // Restore current vitals to new max
                entity.Stats.Hp = entity.Stats.MaxHp;
                entity.Stats.Resource = entity.Stats.MaxResource;
                entity.Stats.Movement = entity.Stats.MaxMovement;
            }
        }

        if (!string.IsNullOrEmpty(template.Race))
        {
            var raceDef = races.Get(template.Race);
            if (raceDef != null)
            {
                entity.SetProperty("race", template.Race);
                foreach (var flag in raceDef.RacialFlags)
                {
                    entity.AddTag(flag);
                }
            }
        }
    }

    private static readonly Regex DicePattern = new(@"^(\d+)d(\d+)([+-]\d+)?$", RegexOptions.IgnoreCase);

    public static int AverageDice(string notation)
    {
        var m = DicePattern.Match(notation.Trim());
        if (!m.Success) { return 0; }
        var count = int.Parse(m.Groups[1].Value);
        var sides = int.Parse(m.Groups[2].Value);
        var mod = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        // Avg of a single dN is (N+1)/2 — use integer math: count * (sides + 1) / 2 + mod
        return count * (sides + 1) / 2 + mod;
    }

    private static void AddToBase(Entity entity, StatType stat, int delta)
    {
        switch (stat)
        {
            case StatType.Strength: entity.Stats.BaseStrength += delta; break;
            case StatType.Intelligence: entity.Stats.BaseIntelligence += delta; break;
            case StatType.Wisdom: entity.Stats.BaseWisdom += delta; break;
            case StatType.Dexterity: entity.Stats.BaseDexterity += delta; break;
            case StatType.Constitution: entity.Stats.BaseConstitution += delta; break;
            case StatType.Luck: entity.Stats.BaseLuck += delta; break;
            case StatType.MaxHp: entity.Stats.BaseMaxHp += delta; break;
            case StatType.MaxResource: entity.Stats.BaseMaxResource += delta; break;
            case StatType.MaxMovement: entity.Stats.BaseMaxMovement += delta; break;
        }
    }
}
