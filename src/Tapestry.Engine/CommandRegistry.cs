using Tapestry.Engine.Registration;
using Tapestry.Shared;

namespace Tapestry.Engine;

public class CommandRegistration
{
    public required string Keyword { get; init; }
    public required Action<ActorContext> ActorHandler { get; init; }
    public string[] Aliases { get; init; } = [];
    public int Priority { get; init; }
    public string PackName { get; init; } = "";
    public int RegistrationOrder { get; init; }

    // Command UI metadata — visibility predicate, categorization, and source tracking
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    /// <summary>Relative path from pack directory. Set automatically by PackLoader; do not supply manually.</summary>
    public string SourceFile { get; init; } = "";
    public Func<Entity, bool>? VisibleTo { get; init; }

    // Role-based access control and argument/GMCP configuration
    public string[] Roles { get; init; } = ["player"];
    public Dictionary<string, ArgDefinition>? ArgDefinitions { get; init; }
    public GmcpConfig? Gmcp { get; init; }
}

public class CommandRegistry
{
    private readonly Dictionary<string, List<CommandRegistration>> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly RegistrationGate? _gate;
    private int _nextOrder;

    public CommandRegistry(RegistrationGate? gate = null)
    {
        _gate = gate;
    }

    public void Register(
        string keyword,
        Action<ActorContext> actorHandler,
        string[]? aliases = null,
        int priority = 0,
        string packName = "",
        string description = "",
        string category = "",
        string sourceFile = "",
        Func<Entity, bool>? visibleTo = null,
        string[]? roles = null,
        Dictionary<string, ArgDefinition>? argDefinitions = null,
        GmcpConfig? gmcp = null)
    {
        _gate?.AssertCommitScope("command", keyword);
        {
            var registration = new CommandRegistration
            {
                Keyword = keyword,
                ActorHandler = actorHandler,
                Aliases = aliases ?? [],
                Priority = priority,
                PackName = packName,
                RegistrationOrder = _nextOrder++,
                Description = description,
                Category = category,
                SourceFile = sourceFile,
                VisibleTo = visibleTo,
                Roles = roles ?? ["player"],
                ArgDefinitions = argDefinitions,
                Gmcp = gmcp
            };

            AddToMap(keyword, registration);
            foreach (var alias in registration.Aliases)
            {
                AddToMap(alias, registration);
            }
        }
    }

    public CommandRegistration? Resolve(string keyword)
    {
        // 1. Exact match (includes explicit aliases)
        if (_commands.TryGetValue(keyword, out var registrations))
        {
            return registrations.OrderByDescending(r => r.Priority).First();
        }

        // 2. Prefix match against primary keywords only
        var prefixMatches = new List<CommandRegistration>();
        foreach (var (key, regs) in _commands)
        {
            if (key.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            {
                // Only match against primary keywords, not aliases
                foreach (var reg in regs)
                {
                    if (reg.Keyword.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        prefixMatches.Add(reg);
                    }
                }
            }
        }

        if (prefixMatches.Count > 0)
        {
            return prefixMatches
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.RegistrationOrder)
                .First();
        }

        return null;
    }

    public CommandRegistration? Resolve(string keyword, string? source)
    {
        if (source == null)
        {
            return Resolve(keyword);
        }

        // Role-aware resolution filters the candidate set by actor type BEFORE electing a
        // winner. Electing first and filtering after let an eagerly-registered mob-only verb
        // win the keyword and then fail the player's role check (tapestry#98).
        // 1. Exact match (includes explicit aliases)
        if (_commands.TryGetValue(keyword, out var registrations))
        {
            var winner = ElectWinner(registrations, source);
            if (winner != null)
            {
                return winner;
            }
            // Keyword exists but nothing is eligible for this actor type; fall through to
            // prefix matching like any other unresolved token.
        }

        // 2. Prefix match against primary keywords only
        var prefixMatches = new List<CommandRegistration>();
        foreach (var (key, regs) in _commands)
        {
            if (key.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            {
                // Only match against primary keywords, not aliases
                foreach (var reg in regs)
                {
                    if (reg.Keyword.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)
                        && MatchesSource(reg, source))
                    {
                        prefixMatches.Add(reg);
                    }
                }
            }
        }

        return prefixMatches.Count > 0 ? ElectWinner(prefixMatches, source) : null;
    }

    // Actor-type roles (who may invoke) vs privilege roles like admin (gated later in
    // CommandRouter). Specificity counts only these — privilege narrowness is not specificity.
    private static readonly HashSet<string> ActorTypeRoles = new(["player", "mob"], StringComparer.OrdinalIgnoreCase);

    private static CommandRegistration? ElectWinner(IEnumerable<CommandRegistration> regs, string source)
    {
        return regs
            .Where(r => MatchesSource(r, source))
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => ActorTypeRoleCount(r)) // actor-type specificity: ["mob"] beats ["player","mob"]
            .ThenBy(r => r.RegistrationOrder)
            .FirstOrDefault();
    }

    private static int ActorTypeRoleCount(CommandRegistration reg)
    {
        var count = reg.Roles.Count(role => ActorTypeRoles.Contains(role));
        // No actor-type roles (e.g. admin-only) = least specific, not most — it must never
        // outrank a registration that actually names the actor type.
        return count == 0 ? int.MaxValue : count;
    }

    private static bool MatchesSource(CommandRegistration reg, string source)
    {
        {
            return source switch
            {
                "mob" => reg.Roles.Contains("mob", StringComparer.OrdinalIgnoreCase),
                "player" => reg.Roles.Contains("player", StringComparer.OrdinalIgnoreCase)
                         || reg.Roles.Contains("admin", StringComparer.OrdinalIgnoreCase),
                _ => true
            };
        }
    }

    public void Unregister(string commandName)
    {
        var key = commandName.ToLower();
        if (_commands.TryGetValue(key, out var registrations))
        {
            foreach (var reg in registrations.ToList())
            {
                if (reg.Keyword.Equals(commandName, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var alias in reg.Aliases)
                    {
                        _commands.Remove(alias.ToLower());
                    }
                }
            }
        }
        _commands.Remove(key);
    }

    public IEnumerable<string> AllKeywords => _commands.Keys;

    // Enumerate registrations, not dictionary keys — keys include alias entries pointing
    // to the same registration. Distinct on Keyword collapses them.
    public IEnumerable<string> PrimaryKeywords =>
        _commands.Values
            .SelectMany(list => list)
            .Select(r => r.Keyword)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private void AddToMap(string key, CommandRegistration registration)
    {
        if (!_commands.TryGetValue(key, out var list))
        {
            list = new List<CommandRegistration>();
            _commands[key] = list;
        }
        list.Add(registration);
    }
}
