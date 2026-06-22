namespace Tapestry.Engine;

/// <summary>Single-delegate registry for lazy-minting a stub exit's target room. The pack
/// registers one resolver; movement calls TryResolve when a player steps into a stub. The
/// resolver mints + two-way-wires the neighbor and returns true. No resolver registered
/// degrades to a graceful refusal, never a crash.</summary>
public sealed class StubExitResolver
{
    private Func<string, string, bool>? _resolver;

    public void Register(Func<string, string, bool> resolver)
    {
        _resolver = resolver;
    }

    public bool HasResolver => _resolver != null;

    public bool TryResolve(string roomId, string direction)
    {
        if (_resolver == null) { return false; }
        try { return _resolver(roomId, direction); }
        catch { return false; }
    }
}
