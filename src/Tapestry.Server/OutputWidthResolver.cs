using Tapestry.Engine;
using Tapestry.Engine.Text;
using Tapestry.Shared;

namespace Tapestry.Server;

/// <summary>
/// Connection→entity adapter: looks up the logged-in player for a raw connection and
/// asks <see cref="OutputWidthService"/> for the effective width. Kept as the single
/// per-connection lookup shared by both decorator construction sites. Pre-login (no
/// session yet) resolves to the configured default.
/// </summary>
public static class OutputWidthResolver
{
    public static int Resolve(IConnection rawConnection, SessionManager sessions, OutputWidthService widthService)
    {
        var player = sessions.GetByConnectionId(rawConnection.Id)?.PlayerEntity;
        return widthService.Resolve(player);
    }
}
