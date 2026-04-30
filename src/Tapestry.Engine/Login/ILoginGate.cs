using Tapestry.Shared;

namespace Tapestry.Engine.Login;

public interface ILoginGate
{
    LoginGateResult Check(string canonicalName, IConnection connection);
}

public enum LoginBlockBehavior
{
    Reprompt,
    Disconnect
}

public record LoginGateResult(
    bool Allowed,
    string? Message = null,
    LoginBlockBehavior Behavior = LoginBlockBehavior.Reprompt)
{
    public static LoginGateResult Allow()
    {
        return new(true);
    }

    public static LoginGateResult Block(string message)
    {
        return new(false, message, LoginBlockBehavior.Reprompt);
    }

    public static LoginGateResult Ban(string message)
    {
        return new(false, message, LoginBlockBehavior.Disconnect);
    }

    public static LoginGateResult Noop()
    {
        return new(false, null, LoginBlockBehavior.Disconnect);
    }
}
