namespace Tapestry.Engine.Help;

/// <summary>
/// Strict (default): the three help gates throw on any violation, failing boot.
/// Lenient: the gates log each violation as a warning and boot continues. Lenient exists
/// for the one-time content migration (read the full violation list, fix until clean, flip
/// back to strict). Sourced from the TAPESTRY_HELP_GATES env var; default is strict.
/// </summary>
public sealed class HelpSealOptions
{
    public bool Strict { get; init; } = true;
}
