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
        // Filled in across A3–A5. For now: exactly one candidate wins.
        return cands[0];
    }
}
