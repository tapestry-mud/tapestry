using Tapestry.Engine;
using Tapestry.Networking;
using Tapestry.Shared;

namespace Tapestry.Server;

/// <summary>
/// Resolves the effective output wrap width for a connection at send time:
/// the player's <c>screen_width</c> preference (if set), else the server config default,
/// clamped to a sane range; then, for telnet, capped down to a narrower NAWS-reported
/// terminal width so we never wrap wider than the actual window. A resolved width of 0
/// disables wrapping. The web client reports no width, so it just uses the cap.
/// </summary>
public static class OutputWidthResolver
{
    public const int MinWidth = 20;
    public const int MaxWidth = 500;

    /// <summary>Connection-aware resolution: reads the player pref off the live session
    /// and the NAWS width off a telnet connection, then defers to the pure overload.</summary>
    public static int Resolve(IConnection rawConnection, SessionManager sessions, int defaultWidth)
    {
        int? playerPref = null;
        var session = sessions.GetByConnectionId(rawConnection.Id);
        // TryGetProperty returns true only when the property is present (it also coerces
        // a JS-authored double to int); on true, `pref` holds the value.
        if (session?.PlayerEntity != null &&
            session.PlayerEntity.TryGetProperty<int>(CommonProperties.ScreenWidth, out var pref))
        {
            playerPref = pref;
        }

        var supportsNaws = false;
        var nawsWidth = 0;
        if (rawConnection is TelnetConnection telnet)
        {
            supportsNaws = telnet.Capabilities.SupportsNaws;
            nawsWidth = telnet.Capabilities.WindowWidth;
        }

        return Resolve(playerPref, supportsNaws, nawsWidth, defaultWidth);
    }

    /// <summary>Pure resolution logic (no connection dependency) for testing.</summary>
    public static int Resolve(int? playerPref, bool supportsNaws, int nawsWidth, int defaultWidth)
    {
        int cap;
        if (playerPref.HasValue)
        {
            if (playerPref.Value <= 0) { return 0; } // player explicitly disabled wrapping
            cap = Math.Clamp(playerPref.Value, MinWidth, MaxWidth);
        }
        else
        {
            if (defaultWidth <= 0) { return 0; } // disabled by config
            cap = Math.Clamp(defaultWidth, MinWidth, MaxWidth);
        }

        // Telnet narrow-floor: never wrap wider than the real terminal window.
        if (supportsNaws && nawsWidth >= MinWidth && nawsWidth < cap)
        {
            cap = nawsWidth;
        }

        return cap;
    }
}
