using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Shared;

namespace Tapestry.Server.Modules;

public class BadInputModule : IGameModule
{
    private readonly CommandRegistry _registry;
    private readonly SessionManager _sessions;
    private readonly BadInputTracker _tracker;
    private readonly RegistrationGate? _gate;

    public string Name => "BadInput";

    public BadInputModule(CommandRegistry registry, SessionManager sessions, BadInputTracker tracker,
        RegistrationGate? gate = null)
    {
        _registry = registry;
        _sessions = sessions;
        _tracker = tracker;
        _gate = gate;
    }

    public void Configure()
    {
        // Kernel-sanctioned write scope: this module's Configure runs AFTER
        // ContentLoadingModule (module registration order in Program.cs), i.e. after the
        // gate arms during pack loading. A C# kernel admin command, not pack content.
        using var scope = _gate?.EnterCommitScope();
        _registry.Register(
            "badinput",
            ctx => HandleBadInput(ctx),
            packName: "engine",
            roles: ["admin"]);
    }

    private void HandleBadInput(ActorContext ctx)
    {
        if (ctx.RawArgs.Length > 0 &&
            string.Equals(ctx.RawArgs[0], "clear", StringComparison.OrdinalIgnoreCase))
        {
            _tracker.Clear();
            _sessions.SendToPlayer(ctx.EntityId, "Bad input log cleared.\r\n");
            return;
        }

        var entries = _tracker.GetEntries();
        if (entries.Count == 0)
        {
            _sessions.SendToPlayer(ctx.EntityId, "No bad input recorded.\r\n");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append($"Bad input log ({entries.Count} unique verbs):\r\n");
        foreach (var entry in entries)
        {
            sb.Append($"  {entry.Verb} ({entry.Count}) -- first seen: {entry.FirstSeen:yyyy-MM-dd HH:mm}, last seen: {entry.LastSeen:yyyy-MM-dd HH:mm}\r\n");
        }
        _sessions.SendToPlayer(ctx.EntityId, sb.ToString());
    }
}
