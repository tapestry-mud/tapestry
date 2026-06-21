namespace Tapestry.Engine.Combat;

/// <summary>
/// The window-validator seam. A pack registers a named deterministic validator through the seal;
/// the swell clock looks it up by the boss's <c>swell_window</c> dial and invokes it at resolve.
/// Validators are always deterministic code (engine or pack JS), never the LLM.
/// </summary>
public class WindowValidatorRegistry
{
    private readonly Dictionary<string, Func<CombatContext, ValidationResult>> _validators = new();

    public void Register(string name, Func<CombatContext, ValidationResult> validator)
    {
        _validators[name] = validator;
    }

    public Func<CombatContext, ValidationResult>? Get(string name)
    {
        return _validators.TryGetValue(name, out var v) ? v : null;
    }
}
