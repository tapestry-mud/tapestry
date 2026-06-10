namespace Tapestry.Engine.Mobs;

/// <summary>
/// Thrown when a mob behavior invocation exceeds its time budget. Raised by the
/// scripting layer's armed Jint constraint (interrupting JS mid-execution) and
/// recognized by MobAIManager as a budget strike, distinct from ordinary behavior
/// exceptions which keep their log-and-continue semantics.
/// </summary>
public class MobBudgetExceededException : Exception
{
    public MobBudgetExceededException(double capMs)
        : base($"Mob behavior exceeded its {capMs}ms invocation cap")
    {
    }
}
