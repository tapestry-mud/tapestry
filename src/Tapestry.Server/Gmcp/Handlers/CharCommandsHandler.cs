using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Help;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class CharCommandsHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly CommandRegistry _commandRegistry;
    private readonly HelpService _helpService;

    public string Name => "CharCommands";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Char.Commands" };

    public CharCommandsHandler(
        IGmcpConnectionManager connectionManager,
        SessionManager sessions,
        World world,
        EventBus eventBus,
        CommandRegistry commandRegistry,
        HelpService helpService)
    {
        _connectionManager = connectionManager;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _commandRegistry = commandRegistry;
        _helpService = helpService;
    }

    public void Configure() { }

    public void SendBurst(string connectionId, object entity)
    {
        var e = (Entity)entity;
        _connectionManager.Send(connectionId, "Char.Commands", BuildPayload(e));
    }

    private object BuildPayload(Entity entity)
    {
        var commands = _commandRegistry.PrimaryKeywords
            .Select(kw => _commandRegistry.Resolve(kw))
            .Where(r => r != null)
            .Select(r => r!)
            .Where(r =>
            {
                if (r.VisibleTo == null) { return true; }
                try { return r.VisibleTo(entity); }
                catch { return false; }
            })
            .Where(r => _helpService.IsListed(r.Keyword))
            .Select(r =>
            {
                var topic = _helpService.GetTopicById(r.Keyword);
                return new
                {
                    keyword = r.Keyword,
                    category = topic?.Category ?? "",
                    description = topic?.Brief ?? "",
                    aliases = r.Aliases,
                };
            })
            .OrderBy(c => c.category)
            .ThenBy(c => c.keyword)
            .ToList();

        return new { commands };
    }
}
