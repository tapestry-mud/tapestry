using Tapestry.Shared;

namespace Tapestry.Engine.Login;

public class LoginGateRegistry
{
    private readonly List<ILoginGate> _gates = new();

    public void Register(ILoginGate gate)
    {
        _gates.Add(gate);
    }

    public LoginGateResult RunAll(string canonicalName, IConnection connection)
    {
        foreach (var gate in _gates)
        {
            var result = gate.Check(canonicalName, connection);
            if (!result.Allowed)
            {
                return result;
            }
        }
        return LoginGateResult.Allow();
    }
}
