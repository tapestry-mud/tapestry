using Tapestry.Engine.Heartbeat;

namespace Tapestry.Engine.Combat;

public class CheckWimpyPhase : ICombatPhase
{
    public string Name => "CheckWimpy";
    public int Priority => 400;

    public void Execute(PulseContext context)
    {
        var combatants = context.CombatManager.GetCombatants().ToList();

        foreach (var entity in combatants)
        {
            if (entity.Stats.Hp <= 0)
            {
                continue;
            }

            var wimpyProp = entity.GetProperty<object>(CombatProperties.WimpyThreshold);
            if (wimpyProp == null)
            {
                continue;
            }

            var threshold = Convert.ToInt32(wimpyProp);
            if (threshold <= 0)
            {
                continue;
            }

            var hpPercent = (int)((double)entity.Stats.Hp / entity.Stats.MaxHp * 100);
            if (hpPercent <= threshold)
            {
                context.CombatManager.AttemptFlee(entity, context);
            }
        }
    }
}
