using Jint;
using Tapestry.Engine.Mobs;
using Tapestry.Scripting;
using Xunit;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Tests;

public class MobInvocationBudgetTests
{
    private static (JintEngine engine, MobInvocationBudget budget) BuildEngine()
    {
        var budget = new MobInvocationBudget();
        var engine = new JintEngine(options =>
        {
            // 2s backstop so a regression fails the test instead of hanging it.
            options.TimeoutInterval(TimeSpan.FromSeconds(2));
            options.Strict();
            options.Constraints.Constraints.Add(budget);
        });
        return (engine, budget);
    }

    [Fact]
    public void ArmedHotLoop_IsInterrupted_AsCatchableBudgetException()
    {
        var (engine, budget) = BuildEngine();
        engine.Execute(
            "function hot() { while (true) { Array.from({ length: 100000 }, function () { return 0; }); } }");
        var hot = engine.GetValue("hot");

        using (budget.Arm(50))
        {
            Assert.Throws<MobBudgetExceededException>(() => engine.Invoke(hot));
        }
    }

    [Fact]
    public void ArmedBulkBuiltIn_SingleStatement_IsInterrupted()
    {
        // The Jint 4.9.3 regression test: ONE statement, all the work inside the
        // built-in. Pre-4.9.3 bulk paths skipped constraint checks and this wedged.
        var (engine, budget) = BuildEngine();
        engine.Execute(
            "function bulk() { Array.from({ length: 50000000 }, function () { return 0; }); }");
        var bulk = engine.GetValue("bulk");

        using (budget.Arm(50))
        {
            Assert.Throws<MobBudgetExceededException>(() => engine.Invoke(bulk));
        }
    }

    [Fact]
    public void Disarmed_NeverThrows()
    {
        var (engine, budget) = BuildEngine();
        engine.Execute("function cheap() { return 1 + 1; }");
        var cheap = engine.GetValue("cheap");

        using (budget.Arm(50))
        {
        } // armed and immediately disarmed

        var result = engine.Invoke(cheap); // no throw
        Assert.Equal(2d, (double)result.ToObject()!);
    }
}
