namespace Tapestry.Engine.Registration;

/// <summary>
/// Structural enforcement for the registration seal. Once pack loading begins (Arm), any
/// policy-guarded registry write outside a RegistrationPolicy commit scope is a boot error.
/// Turns "registries route through the policy" from a convention into a wall -- the wall the
/// v0.1.20 mobs.registerCommand bypass walked around (tapestry#98). Disarmed by default:
/// engine C# boot registrations and direct-construction unit tests are unaffected.
/// </summary>
public sealed class RegistrationGate
{
    private bool _armed;
    private int _commitDepth;   // boot + seal are single-threaded; no interlock needed

    public void Arm()
    {
        _armed = true;
    }

    public IDisposable EnterCommitScope()
    {
        _commitDepth++;
        return new CommitScope(this);
    }

    public void AssertCommitScope(string kind, string name)
    {
        if (_armed && _commitDepth <= 0)
        {
            throw new InvalidOperationException(
                $"{kind} '{name}': direct registry write bypasses RegistrationPolicy. " +
                "Once pack loading starts, registry writes are only legal inside the policy's " +
                "commit scope -- Record a RegistrationCandidate instead.");
        }
    }

    private sealed class CommitScope(RegistrationGate gate) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            // A double-dispose would underflow the depth and silently disarm the wall
            // for every later legitimate scope. Make the scope idempotent instead.
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            gate._commitDepth--;
        }
    }
}
