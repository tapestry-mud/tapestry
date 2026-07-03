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
            if (context.CombatManager.ShouldFlee(entity, context.CurrentTick))
            {
                context.CombatManager.AttemptFlee(entity, context);
            }
        }
    }
}
