namespace Tapestry.Engine;

/// <summary>
/// Tracks whether the current thread is executing inside <see cref="GameLoop.Tick"/>.
///
/// The engine hosts one Jint engine for all pack script, and Jint has no synchronization of
/// its own. Everything the loop does -- commands, tick handlers, event dispatch -- is
/// serialized by Tick, so script execution is safe there and only there. Login, by contrast,
/// resolves on a thread-pool thread; when it published an engine event directly, the script
/// dispatch that followed ran concurrently with the loop's own and tore a Jint call in
/// progress (observed on prod as a NullReferenceException raised inside
/// Jint.Native.Function.ScriptFunction.Call under character.created, which silently voided the
/// content-side login hook for about one connect in ten).
///
/// Tick is fully synchronous, so a thread-static flag is an exact answer: it is set for
/// precisely the span of one tick on whichever pool thread the loop is running on. Callers use
/// this only to report a violation -- it never changes what runs where.
/// </summary>
public static class LoopAffinity
{
    [ThreadStatic]
    private static bool _inTick;

    private static int _loopStarted;

    /// <summary>True while the calling thread is inside a game-loop tick.</summary>
    public static bool OnLoop => _inTick;

    /// <summary>
    /// True once the loop has ticked at least once. Guards the diagnostic: unit tests and boot
    /// legitimately drive engine services with no loop running, and those are not violations.
    /// </summary>
    public static bool LoopStarted => Volatile.Read(ref _loopStarted) == 1;

    public static void BeginTick()
    {
        _inTick = true;
        Volatile.Write(ref _loopStarted, 1);
    }

    public static void EndTick()
    {
        _inTick = false;
    }
}
