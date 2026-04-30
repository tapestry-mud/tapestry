using Tapestry.Engine.Combat;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class DiceModule : IJintApiModule
{
    public string Namespace => "dice";

    public object Build(JintEngine engine)
    {
        return new
        {
            roll = new Func<string, int>((notation) =>
            {
                return DiceRoller.Roll(notation);
            }),

            rollD20 = new Func<int>(() =>
            {
                return DiceRoller.RollD20();
            })
        };
    }
}
