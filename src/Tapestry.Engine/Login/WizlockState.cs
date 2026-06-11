namespace Tapestry.Engine.Login;

/// <summary>
/// Runtime-only wizlock flag (ROM parity). While locked, non-admin logins are
/// refused with <see cref="Message"/>; characters that already hold the admin
/// role log in normally, and already-connected players are never kicked --
/// enforcement lives only on the login paths. Deliberately NOT persisted:
/// like ROM's wizlock, the flag resets to unlocked on reboot.
/// </summary>
public sealed class WizlockState
{
    public const string Message = "The game is wizlocked.";

    public bool Locked { get; set; }
}
