using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Server;

/// <summary>
/// A character created through the chargen flow is promoted to <c>Playing</c> in place by
/// FlowEngine, which - unlike <c>PlayerSpawner.CompleteLogin</c> for a normal login - never
/// installs the disconnect teardown. Without it the new player's very first quit enqueues no
/// <see cref="DisconnectEvent"/>, so GameLoopService never removes the session: it leaks in
/// <see cref="SessionManager"/> until reboot (the connection sits idle, and a reconnect with
/// the same name is met with "take over the session").
///
/// This bridge listens for the <c>character.created</c> event that FlowEngine publishes at the
/// moment creation completes and installs the standard teardown on that session's connection.
/// One subscription covers both the telnet and the web pre-auth creation paths, since both
/// complete through the same flow. The Creating-gated handler at the creation site still
/// handles mid-creation abandon (no-save), which is why this only wires once creation is done.
/// </summary>
public class NewCharacterTeardownBridge
{
    private readonly SessionManager _sessions;
    private readonly SystemEventQueue _eventQueue;

    public NewCharacterTeardownBridge(EventBus eventBus, SessionManager sessions, SystemEventQueue eventQueue)
    {
        _sessions = sessions;
        _eventQueue = eventQueue;
        eventBus.Subscribe("character.created", OnCharacterCreated);
    }

    private void OnCharacterCreated(GameEvent evt)
    {
        if (evt.SourceEntityId is not Guid entityId)
        {
            return;
        }

        var session = _sessions.GetByEntityId(entityId);
        if (session == null)
        {
            return;
        }

        ConnectionTeardown.Wire(session.Connection, entityId, _eventQueue);
    }
}
