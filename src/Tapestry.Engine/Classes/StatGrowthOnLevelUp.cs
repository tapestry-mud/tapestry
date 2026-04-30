using Tapestry.Engine.Combat;
using Tapestry.Engine.Stats;
using Tapestry.Engine.Training;
using Tapestry.Shared;

namespace Tapestry.Engine.Classes;

public static class StatGrowthOnLevelUp
{
    public static void Subscribe(EventBus eventBus, World world, ClassRegistry classes,
        TrainingManager trainingManager, Random? random = null)
    {
        var rng = random ?? Random.Shared;
        eventBus.Subscribe("progression.level.up", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            var entity = world.GetEntity(evt.SourceEntityId.Value);
            if (entity == null) { return; }

            var classId = entity.GetProperty<string>("class");
            if (string.IsNullOrEmpty(classId)) { return; }

            var def = classes.Get(classId);
            if (def == null) { return; }

            foreach (var kv in def.StatGrowth)
            {
                var roll = DiceRoller.Roll(kv.Value, rng);
                var bonus = 0;
                if (def.GrowthBonuses.TryGetValue(kv.Key, out var sourceAttr))
                {
                    var attrValue = GetAttributeValue(entity, sourceAttr);
                    bonus = Math.Max(0, (attrValue - 10) / 2);
                }
                ApplyStatDelta(entity, kv.Key, roll + bonus);
            }

            trainingManager.GrantTrains(entity.Id, def.TrainsPerLevel);
        });
    }

    private static int GetAttributeValue(Entity entity, StatType attr) => attr switch
    {
        StatType.Strength => entity.Stats.Strength,
        StatType.Intelligence => entity.Stats.Intelligence,
        StatType.Wisdom => entity.Stats.Wisdom,
        StatType.Dexterity => entity.Stats.Dexterity,
        StatType.Constitution => entity.Stats.Constitution,
        StatType.Luck => entity.Stats.Luck,
        _ => 10
    };

    private static void ApplyStatDelta(Entity entity, StatType stat, int delta)
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
