using System;
using Tapestry.Engine;
using Tapestry.Engine.Color;
using Tapestry.Engine.Text;
using Tapestry.Engine.Watch;
using Tapestry.Shared;

namespace Tapestry.Server;

/// <summary>
/// Single source of truth for assembling a player's output decorator chain
/// (ColorRendering -> Tee -> Wrapping -> raw). Both connection entry points
/// (ConnectionHandler for telnet, the WebSocket pre-auth branch in Program.cs) call this
/// so the TeeConnection is present on EVERY player chain.
/// </summary>
public static class OutputChainFactory
{
    public static IConnection Build(
        IConnection raw,
        ColorRenderer colorRenderer,
        OutputWrapper outputWrapper,
        OutputWidthService outputWidthService,
        SessionManager sessions,
        WatchRegistry watch)
    {
        var wrapping = new WrappingConnection(
            raw,
            outputWrapper,
            () => OutputWidthResolver.Resolve(raw, sessions, outputWidthService));

        var tee = new TeeConnection(
            wrapping,
            watch,
            () => sessions.GetByConnectionId(raw.Id)?.PlayerEntity?.Id);

        return new ColorRenderingConnection(tee, colorRenderer);
    }
}
