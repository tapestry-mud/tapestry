using Tapestry.Shared;

namespace Tapestry.Engine;

public class CommandRouter
{
    private readonly CommandRegistry _registry;
    private readonly SessionManager _sessions;

    public CommandRouter(CommandRegistry registry, SessionManager sessions)
    {
        _registry = registry;
        _sessions = sessions;
    }

    public void Route(CommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Command))
        {
            return;
        }

        var registration = _registry.Resolve(ctx.Command);
        if (registration == null)
        {
            _sessions.SendToPlayer(ctx.PlayerEntityId, "Huh?\r\n");
            return;
        }

        registration.Handler(ctx);
    }

    public CommandRegistration? Resolve(string keyword)
    {
        return _registry.Resolve(keyword);
    }
}
