using System;
using System.Collections.Generic;
using System.Linq;

namespace Tapestry.Engine.Registration;

/// <summary>One accumulated registration intent. Commit replays the real registry write at Resolve() time.</summary>
public sealed record RegistrationCandidate(
    string Kind,        // "tick", "command", ... (the registry family)
    string Name,        // the colliding key within that family
    string Owner,       // pack namespace, or a non-overridable owner ("kernel"/"engine"/"tapestry-core")
    bool IsOverride,    // author declared { override: true }
    Action Commit,      // replays the underlying registry's Register call
    string SourceFile,  // for located diagnostics; "" when N/A (e.g. C# kernel registrations)
    int Line);

/// <summary>
/// Declarative registration ledger. All boot-phase register/override calls Record() a candidate;
/// Resolve() (run once at the seal barrier) picks an order-independent winner per (kind, name)
/// and replays its Commit. Collisions are boot errors unless one candidate declares an edge-valid override.
/// </summary>
public sealed class RegistrationPolicy
{
    private static readonly HashSet<string> NonOverridableOwners =
        new(StringComparer.OrdinalIgnoreCase) { "kernel", "engine", "tapestry-core" };

    private readonly List<RegistrationCandidate> _candidates = new();
    private readonly IPackEdgeOracle _edges;
    private bool _sealed;

    public RegistrationPolicy(IPackEdgeOracle edges) => _edges = edges;

    public bool IsSealed => _sealed;

    public void Record(RegistrationCandidate candidate)
    {
        _candidates.Add(candidate);
    }

    public void Resolve()
    {
        var groups = _candidates.GroupBy(c => (c.Kind, Name: c.Name.ToLowerInvariant()));
        var winners = new List<RegistrationCandidate>();
        foreach (var group in groups)
        {
            winners.Add(ResolveGroup(group.ToList()));
        }
        foreach (var winner in winners)
        {
            winner.Commit();
        }
        _sealed = true;
    }

    private RegistrationCandidate ResolveGroup(List<RegistrationCandidate> cands)
    {
        var bases = cands.Where(c => !c.IsOverride).ToList();
        var overrides = cands.Where(c => c.IsOverride).ToList();

        if (overrides.Count == 0 && bases.Count == 1)
        {
            return bases[0];
        }

        if (overrides.Count == 0 && bases.Count > 1)
        {
            throw Conflict(bases[1],
                $"two packs register {bases[0].Kind} '{bases[0].Name}' " +
                $"({string.Join(", ", bases.Select(b => b.Owner))}); " +
                $"declare {{ override: true }} on the intended winner or rename one");
        }

        if (overrides.Count > 0 && bases.Count == 0)
        {
            throw Conflict(overrides[0],
                $"{overrides[0].Owner} declares override of {overrides[0].Kind} '{overrides[0].Name}' " +
                "but no base exists to override");
        }

        if (bases.Count == 1 && overrides.Count >= 1)
        {
            if (overrides.Count > 1)
            {
                throw Conflict(overrides[1],
                    $"two packs both override {overrides[0].Kind} '{overrides[0].Name}' " +
                    $"({string.Join(", ", overrides.Select(o => o.Owner))})");
            }

            var winner = overrides[0];
            var ownerOfBase = bases[0].Owner;

            if (NonOverridableOwners.Contains(ownerOfBase))
            {
                throw Conflict(winner,
                    $"{winner.Kind} '{winner.Name}' is owned by '{ownerOfBase}' (engine/kernel) " +
                    "and is not pack-overridable");
            }

            if (!string.Equals(winner.Owner, ownerOfBase, StringComparison.OrdinalIgnoreCase)
                && !_edges.DeclaresEdge(winner.Owner, ownerOfBase))
            {
                throw Conflict(winner,
                    $"{winner.Owner} overrides {winner.Kind} '{winner.Name}' owned by '{ownerOfBase}' " +
                    "but declares no dependency on it; add it to `dependencies`");
            }

            return winner;
        }

        throw Conflict(cands[0],
            $"ambiguous registration for {cands[0].Kind} '{cands[0].Name}' " +
            $"({bases.Count} base(s), {overrides.Count} override(s))");
    }

    private static InvalidOperationException Conflict(RegistrationCandidate at, string reason)
    {
        // Mirror the existing located-diagnostic format: "{pack} ({file}:{line}): <reason>"
        // (PackValidator.cs:392-394). file/line omitted for C# kernel registrations.
        var loc = string.IsNullOrEmpty(at.SourceFile) ? at.Owner : $"{at.Owner} ({at.SourceFile}:{at.Line})";
        return new InvalidOperationException($"{loc}: {reason}");
    }
}
