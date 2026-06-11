using Tapestry.Engine.Login;
using Tapestry.Shared;

namespace Tapestry.Server.Login;

/// <summary>
/// Refuses NEW character creation while wizlocked. Login gates only run on the
/// new-character paths (telnet creation flow plus the /auth/select and
/// /auth/register endpoints), where the character cannot already hold the
/// admin role -- so a name-blind check is sufficient here. Existing characters
/// are enforced after their save loads and roles are known: see
/// LoginFlow.HandleExistingPlayerAsync and the pre-auth token redemption in
/// Program.cs.
/// </summary>
public class WizlockGate : ILoginGate
{
    private readonly WizlockState _wizlock;

    public WizlockGate(WizlockState wizlock)
    {
        _wizlock = wizlock;
    }

    public LoginGateResult Check(string canonicalName, IConnection connection)
    {
        if (_wizlock.Locked)
        {
            // Ban => message + disconnect, matching ROM (wizlock closes the link).
            return LoginGateResult.Ban(WizlockState.Message);
        }
        return LoginGateResult.Allow();
    }
}
