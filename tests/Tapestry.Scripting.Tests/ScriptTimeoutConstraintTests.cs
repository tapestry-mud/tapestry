using FluentAssertions;
using Jint;

using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Tests;

public class ScriptTimeoutConstraintTests
{
    private static JintEngine EngineWithBudget(TimeSpan budget) =>
        new(options => options.Constraints.Constraints.Add(new ScriptTimeoutConstraint(budget)));

    [Fact]
    public void InfiniteLoop_ThrowsScriptTimeoutException_NamingBudgetAndElapsed()
    {
        var engine = EngineWithBudget(TimeSpan.FromMilliseconds(100));

        var act = () => engine.Execute("while (true) {}");

        act.Should().Throw<ScriptTimeoutException>()
            .Where(e => e.Budget == TimeSpan.FromMilliseconds(100))
            .Where(e => e.Elapsed >= TimeSpan.FromMilliseconds(100))
            .WithMessage("*Jint execution budget*")
            .WithMessage("*100ms*");
    }

    [Fact]
    public void ScriptTimeoutException_IsATimeoutException_SoGenericCatchPathsAreUnchanged()
    {
        new ScriptTimeoutException(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6))
            .Should().BeAssignableTo<TimeoutException>();
    }

    [Fact]
    public void FastScript_DoesNotThrow()
    {
        var engine = EngineWithBudget(TimeSpan.FromSeconds(5));

        var act = () => engine.Execute("var x = 1 + 1;");

        act.Should().NotThrow();
    }
}
