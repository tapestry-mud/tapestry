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

    public HelpSeal(
        HelpService help,
        CommandRegistry commands,
        IPackEdgeOracle edges,
        IReadOnlyList<AuthoredHelpRecord>? authoredWinners = null)
    {
        _help = help;
        _commands = commands;
        _edges = edges;
        // Default to the help service's own captured winners (production path); tests may inject.
        _authoredWinners = authoredWinners ?? help.AuthoredWinners;
    }

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
            // non-pack owner (empty, or "kernel"/"engine"). No pack owns them exclusively, so any
            // pack - typically @tapestry/core - may document them. The shadow gate only governs one
            // pack documenting ANOTHER PACK's command, so exempt non-pack owners here.
            if (string.IsNullOrWhiteSpace(commandOwner)
                || string.Equals(commandOwner, "kernel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandOwner, "engine", StringComparison.OrdinalIgnoreCase))
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
    }
}
