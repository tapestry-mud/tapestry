using FluentAssertions;
using Tapestry.Engine.Registration;
using Xunit;

namespace Tapestry.Engine.Tests.Registration;

public class RegistrationPolicyTests
{
    // Edge oracle fake: edges is a set of "from->to" pairs.
    private sealed class FakeEdges : IPackEdgeOracle
    {
        private readonly HashSet<(string, string)> _edges = new();
        public FakeEdges Edge(string from, string to) { _edges.Add((from, to)); return this; }
        public bool DeclaresEdge(string from, string to) => _edges.Contains((from, to));
    }

    private static RegistrationPolicy NewPolicy(IPackEdgeOracle? edges = null)
        => new(edges ?? new FakeEdges());

    [Fact]
    public void SingleRegistration_CommitsExactlyOnce_AtResolve()
    {
        var policy = NewPolicy();
        var committed = 0;

        policy.Record(new RegistrationCandidate("command", "look", "pack-a", false, () => committed++, "scripts/a.js", 1));
        committed.Should().Be(0, "candidates must not commit until Resolve()");

        policy.Resolve();
        committed.Should().Be(1);
    }
}
