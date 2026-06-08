using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Server;

/// <summary>
/// Resolves the effective output wrap width for a connection at send time: the player's
/// <c>screen_width</c> preference if set, otherwise the server config default, clamped to a
/// sane range. A resolved width of 0 disables wrapping.
///
/// We deliberately do NOT auto-narrow to the telnet NAWS-reported terminal size: a fixed
/// readability width is the goal, and auto-narrowing re-wrapped fixed-width panels/maps and
/// the character-creation wizard on narrow clients. Players who want a narrower wrap set it
/// explicitly via the <c>width</c> command (which they understand may reflow panels).
/// </summary>
public static class OutputWidthResolver
{
    public const int MinWidth = 20;
    public const int MaxWidth = 500;

    /// <summary>Connection-aware resolution: reads the player pref off the live session,
    /// then defers to the pure overload.</summary>
    public static int Resolve(IConnection rawConnection, SessionManager sessions, int defaultWidth)
    {
        int? playerPref = null;
        var session = sessions.GetByConnectionId(rawConnection.Id);
        // TryGetProperty returns true only when the property is present (it also coerces a
        // JS-authored double to int); on true, `pref` holds the value.
        if (session?.PlayerEntity != null &&
            session.PlayerEntity.TryGetProperty<int>(CommonProperties.ScreenWidth, out var pref))
        {
            playerPref = pref;
        }

        return Resolve(playerPref, defaultWidth);
    }

    /// <summary>Pure resolution logic (no connection dependency) for testing.</summary>
    public static int Resolve(int? playerPref, int defaultWidth)
    {
        if (playerPref.HasValue)
        {
            if (playerPref.Value <= 0) { return 0; } // player explicitly disabled wrapping
            return Math.Clamp(playerPref.Value, MinWidth, MaxWidth);
        }

        if (defaultWidth <= 0) { return 0; } // disabled by config
        return Math.Clamp(defaultWidth, MinWidth, MaxWidth);
    }
}
