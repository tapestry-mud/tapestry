using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Server;

/// <summary>
/// Single source of truth for the standard player disconnect teardown: on disconnect the
/// connection enqueues a <see cref="DisconnectEvent"/>, which GameLoopService turns into the
/// save + link-dead/quit + <c>SessionManager.Remove</c> path. Both the normal login path
/// (<c>PlayerSpawner.CompleteLogin</c>) and the new-character path
/// (<see cref="NewCharacterTeardownBridge"/>) wire through here so the two cannot drift.
/// </summary>
public static class ConnectionTeardown
{
    public static void Wire(IConnection connection, Guid entityId, SystemEventQueue eventQueue)
    {
        connection.OnDisconnectedWithReason += (reason) =>
            eventQueue.Enqueue(new DisconnectEvent(
                Guid.Parse(connection.Id), entityId, reason ?? "connection closed"));
    }
}
