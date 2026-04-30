using Tapestry.Engine.Login;
using Tapestry.Shared;

namespace Tapestry.Server.Login;

public class ReservedNameGate : ILoginGate
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "self", "me", "all", "here", "nobody", "admin", "system"
    };

    public LoginGateResult Check(string canonicalName, IConnection connection)
    {
        if (ReservedNames.Contains(canonicalName))
        {
            return LoginGateResult.Block("That name is reserved. Try another.");
        }
        return LoginGateResult.Allow();
    }
}
