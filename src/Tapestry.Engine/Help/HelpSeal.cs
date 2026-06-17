using Microsoft.Extensions.Logging;
using Tapestry.Engine.Registration;

namespace Tapestry.Engine.Help;

/// <summary>
/// Post-seal help resolution (runs AFTER RegistrationPolicy.Resolve(), so the CommandRegistry is
/// committed). Two passes:
///   1. Command-shadowing authority - help shadows commands, so a hand-authored help topic whose id
///      matches a resolved command keyword owned by a DIFFERENT pack must declare { override: true }
///      and a dependency edge on the command owner, else boot error. Same owner is implicitly fine.
///   2. Syntax stamping - stamp the args-derived syntax line onto each authored command topic so
///      authors never hand-write syntax.
/// This is the slice's single focused engine addition; it never touches RegistrationPolicy's table.
/// </summary>
public sealed class HelpSeal
{
    private readonly HelpService _help;
    private readonly CommandRegistry _commands;
    private readonly IPackEdgeOracle _edges;
    private readonly IReadOnlyList<AuthoredHelpRecord> _authoredWinners;
    private readonly HelpSealOptions _options;
    private readonly ILogger<HelpSeal>? _logger;

    // Primary constructor.
    public HelpSeal(
        HelpService help,
        CommandRegistry commands,
        IPackEdgeOracle edges,
        IReadOnlyList<AuthoredHelpRecord> authoredWinners,
        HelpSealOptions options,
        ILogger<HelpSeal>? logger)
    {
        _help = help;
        _commands = commands;
        _edges = edges;
        _authoredWinners = authoredWinners;
        _options = options;
        _logger = logger;
    }

    // Back-compat for existing tests and production DI path (defaults to strict, no logger).
    public HelpSeal(
        HelpService help,
        CommandRegistry commands,
        IPackEdgeOracle edges,
        IReadOnlyList<AuthoredHelpRecord>? authoredWinners = null)
        : this(help, commands, edges, authoredWinners ?? help.AuthoredWinners, new HelpSealOptions(), null) { }

    public void Seal()
    {
        // Resolved command keyword -> owner (EXACT keyword match only; Resolve() prefix-matches,
        // which would false-trigger, so build the map from primary keywords).
        var commandOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var registrations = new List<CommandRegistration>();
        foreach (var keyword in _commands.PrimaryKeywords)
        {
            var reg = _commands.Resolve(keyword);
            if (reg == null) { continue; }
            commandOwners[reg.Keyword] = reg.PackName;
            registrations.Add(reg);
        }

        // Pass 1: command-shadowing authority.
        foreach (var authored in _authoredWinners)
        {
            if (!commandOwners.TryGetValue(authored.Id, out var commandOwner)) { continue; }
            // Engine/kernel-registered commands (C# modules - e.g. emote, socials, say) carry a
            // non-pack owner (empty, or "kernel"/"engine"/"core"). No pack owns them exclusively, so
            // any pack - typically @tapestry/core - may document them. The shadow gate only governs
            // one pack documenting ANOTHER PACK's command, so exempt non-pack owners here.
            if (string.IsNullOrWhiteSpace(commandOwner)
                || string.Equals(commandOwner, "kernel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandOwner, "engine", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandOwner, "core", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (string.Equals(authored.Owner, commandOwner, StringComparison.OrdinalIgnoreCase))
            {
                continue; // a pack enriching its own command's help — implicit, fine
            }
            if (!authored.IsOverride || !_edges.DeclaresEdge(authored.Owner, commandOwner))
            {
                var loc = string.IsNullOrEmpty(authored.SourceFile)
                    ? authored.Owner
                    : $"{authored.Owner} ({authored.SourceFile})";
                throw new InvalidOperationException(
                    $"{loc}: help topic '{authored.Id}' shadows the '{authored.Id}' command owned by " +
                    $"'{commandOwner}'. Declare {{ override: true }} on the topic and add '{commandOwner}' " +
                    "to this pack's dependencies, or rename the topic.");
            }
        }

        // Pass 2 (was auto-gen gap-fill): stamp args-derived syntax onto authored command topics.
        CommandHelpGenerator.StampSyntax(registrations, _help);

        var violations = new List<string>();
        var authoredIds = new HashSet<string>(_authoredWinners.Select(w => w.Id), StringComparer.OrdinalIgnoreCase);

        // Gate 1: coverage - every registered command needs an authored topic.
        foreach (var reg in registrations)
        {
            if (!authoredIds.Contains(reg.Keyword))
            {
                violations.Add($"command '{reg.Keyword}' (owner '{reg.PackName}') has no help topic; author help/{reg.Keyword}.yaml");
            }
        }

        // Gate 2: category - every authored topic's category must be declared.
        // Only enforced when at least one category has been registered; an empty vocabulary
        // means the category system has not been seeded yet (migration path).
        var declared = _help.DeclaredCategoryIds;
        if (declared.Count > 0)
        {
            var declaredSet = new HashSet<string>(declared, StringComparer.OrdinalIgnoreCase);
            foreach (var w in _authoredWinners)
            {
                var topic = _help.GetTopicById(w.Id);
                if (topic == null) { continue; }
                if (string.IsNullOrWhiteSpace(topic.Category) || !declaredSet.Contains(topic.Category))
                {
                    var suggestion = NearestCategory(topic.Category, declared);
                    var hint = suggestion == null ? "" : $" - did you mean '{suggestion}'?";
                    var loc = string.IsNullOrEmpty(w.SourceFile) ? w.Owner : $"{w.Owner} ({w.SourceFile})";
                    violations.Add($"{loc}: topic '{w.Id}' has unknown category '{topic.Category}'{hint}; declared: [{string.Join(", ", declared)}]");
                }
            }
        }

        // Gate 3: see-also - every reference must resolve in the merged topic set (loose).
        var allTopicIds = _help.AllTopicIds; // HashSet, case-insensitive
        foreach (var w in _authoredWinners)
        {
            var topic = _help.GetTopicById(w.Id);
            if (topic == null) { continue; }
            foreach (var refId in topic.SeeAlso)
            {
                if (string.IsNullOrWhiteSpace(refId)) { continue; }
                if (!allTopicIds.Contains(refId))
                {
                    var loc = string.IsNullOrEmpty(w.SourceFile) ? w.Owner : $"{w.Owner} ({w.SourceFile})";
                    violations.Add($"{loc}: topic '{w.Id}' see_also references '{refId}' which is not a registered topic");
                }
            }
        }

        ReportViolations(violations);
    }

    private void ReportViolations(List<string> violations)
    {
        if (violations.Count == 0) { return; }
        if (_options.Strict)
        {
            throw new InvalidOperationException(
                "help registry seal failed:\r\n  - " + string.Join("\r\n  - ", violations));
        }
        foreach (var v in violations)
        {
            _logger?.LogWarning("help seal (lenient): {Violation}", v);
        }
    }

    private static string? NearestCategory(string? value, IReadOnlyList<string> declared)
    {
        if (string.IsNullOrWhiteSpace(value) || declared.Count == 0) { return null; }
        string? best = null;
        var bestDist = int.MaxValue;
        foreach (var cand in declared)
        {
            var d = Levenshtein(value.ToLowerInvariant(), cand.ToLowerInvariant());
            if (d < bestDist) { bestDist = d; best = cand; }
        }
        // Always surface the nearest category when one exists; in a small finite vocabulary,
        // showing the closest option is always more useful than silently omitting the hint.
        return best;
    }

    private static int Levenshtein(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) { dp[i, 0] = i; }
        for (var j = 0; j <= b.Length; j++) { dp[0, j] = j; }
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
            }
        }
        return dp[a.Length, b.Length];
    }
}
