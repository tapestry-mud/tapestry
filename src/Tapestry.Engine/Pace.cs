namespace Tapestry.Engine;

/// <summary>
/// Which clock a command obeys. <see cref="Free"/> fires immediately, real-time, always
/// (even through a frozen swell). <see cref="Battle"/> is subject to the combat clock.
/// Default is <see cref="Free"/> so existing commands are unchanged.
/// </summary>
public enum Pace
{
    Free,
    Battle
}
