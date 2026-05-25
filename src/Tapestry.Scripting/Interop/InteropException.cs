namespace Tapestry.Scripting.Interop;

/// <summary>
/// Thrown by tapestry.packs.call/has when an interop rule is violated:
/// an undeclared dependency edge, an unknown target pack, or an unknown export.
/// Propagates as an ordinary command error (contained per-command by GameLoop).
/// </summary>
public sealed class InteropException : Exception
{
    public InteropException(string message) : base(message) { }
}
