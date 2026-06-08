using Tapestry.Data;

namespace Tapestry.Engine.Text;

/// <summary>
/// Single source of truth for a player's effective output width. Used by both the
/// word-wrapper (prose) and Jint (frames), so the two can never disagree. Resolution:
/// the player's screen_width preference if set (>0), else the server config default,
/// clamped to [MinWidth, MaxWidth]. A result of 0 means wrapping is disabled / unbounded.
/// </summary>
public sealed class OutputWidthService
{
    public const int MinWidth = 20;
    public const int MaxWidth = 500;

    private readonly ServerConfig _config;

    public OutputWidthService(ServerConfig config) => _config = config;

    /// <summary>The configured default wrap width (server.yaml output.wrap_width).</summary>
    public int Default => _config.Output.WrapWidth;

    /// <summary>Effective width for a player (null/unknown entity → default).</summary>
    public int Resolve(Entity? player)
    {
        int? pref = null;
        if (player != null && player.TryGetProperty<int>(CommonProperties.ScreenWidth, out var p))
        {
            pref = p;
        }
        return Resolve(pref, Default);
    }

    /// <summary>Pure resolution (no entity dependency) for testing and reuse.</summary>
    public static int Resolve(int? playerPref, int defaultWidth)
    {
        if (playerPref.HasValue)
        {
            if (playerPref.Value <= 0) { return 0; }
            return Math.Clamp(playerPref.Value, MinWidth, MaxWidth);
        }
        if (defaultWidth <= 0) { return 0; }
        return Math.Clamp(defaultWidth, MinWidth, MaxWidth);
    }
}
