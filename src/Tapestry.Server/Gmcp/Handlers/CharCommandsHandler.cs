using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class CharCommandsHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly CommandRegistry _commandRegistry;

    public string Name => "CharCommands";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Char.Commands" };

    private static readonly Dictionary<string, string> KeywordCategoryOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["commands"] = "utility",
            ["consider"] = "combat",
            ["flee"]     = "combat",
            ["kill"]     = "combat",
            ["wimpy"]    = "combat",
        };

    public CharCommandsHandler(
        IGmcpConnectionManager connectionManager,
        SessionManager sessions,
        World world,
        EventBus eventBus,
        CommandRegistry commandRegistry)
    {
        _connectionManager = connectionManager;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _commandRegistry = commandRegistry;
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
            .Select(r =>
            {
                var raw = !string.IsNullOrEmpty(r.Category) ? r.Category : DeriveCategory(r.SourceFile);
                var normalized = NormalizeCategory(raw);
                var category = KeywordCategoryOverrides.TryGetValue(r.Keyword, out var kw) ? kw : normalized;
                return new
                {
                    keyword = r.Keyword,
                    category,
                    description = r.Description,
                    aliases = r.Aliases,
                };
            })
            .OrderBy(c => c.category)
            .ThenBy(c => c.keyword)
            .ToList();

        return new { commands };
    }

    private static string NormalizeCategory(string raw) => raw.ToLower() switch
    {
        "close" or "open" or "lock" or "unlock" => "objects",
        "drink" or "eat" or "fill" or "quaff" or "recite" or "donate" => "items",
        "enter" or "leave" => "movement",
        "time" or "weather" or "information" => "utility",
        "train" or "tree" or "practice" or "list" => "progression",
        _ => raw,
    };

    private static string DeriveCategory(string sourceFile)
    {
        if (string.IsNullOrEmpty(sourceFile)) { return "misc"; }
        var normalized = sourceFile.Replace('\\', '/');
        if (normalized.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["scripts/".Length..];
        }
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash <= 0) { return "misc"; }
        var fileName = normalized[(lastSlash + 1)..];
        var dotIndex = fileName.LastIndexOf('.');
        var stem = dotIndex >= 0 ? fileName[..dotIndex] : fileName;
        return string.IsNullOrEmpty(stem) ? "misc" : stem.ToLower();
    }
}
