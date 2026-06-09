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

    [Fact]
    public void TwoRegistrations_NoOverride_ThrowAtResolve_NotAtRecord()
    {
        var policy = NewPolicy();
        policy.Record(new RegistrationCandidate("command", "look", "pack-a", false, () => { }, "a.js", 1));
        policy.Record(new RegistrationCandidate("command", "look", "pack-b", false, () => { }, "b.js", 2)); // Record must NOT throw

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("look").And.Contain("override");
    }

    [Fact]
    public void Collision_ResolvesIdentically_RegardlessOfArrivalOrder()
    {
        // Reverse the arrival order; the outcome (a throw) must be the same — arrival never decides.
        var policy = NewPolicy();
        policy.Record(new RegistrationCandidate("command", "look", "pack-b", false, () => { }, "b.js", 2));
        policy.Record(new RegistrationCandidate("command", "look", "pack-a", false, () => { }, "a.js", 1));
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void SameNameDifferentKind_DoNotCollide()
    {
        var policy = NewPolicy();
        policy.Record(new RegistrationCandidate("command", "look", "pack-a", false, () => { }, "a.js", 1));
        policy.Record(new RegistrationCandidate("tick", "look", "kernel", false, () => { }, "", 0));
        policy.Invoking(p => p.Resolve()).Should().NotThrow();
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins_OverBase()
    {
        var edges = new FakeEdges().Edge("pack-b", "pack-a"); // pack-b depends on pack-a
        var policy = NewPolicy(edges);
        var baseFired = 0; var overrideFired = 0;

        // Override recorded FIRST, base SECOND — winner must not depend on arrival order.
        policy.Record(new RegistrationCandidate("command", "look", "pack-b", true,  () => overrideFired++, "b.js", 1));
        policy.Record(new RegistrationCandidate("command", "look", "pack-a", false, () => baseFired++,     "a.js", 1));

        policy.Resolve();
        overrideFired.Should().Be(1);
        baseFired.Should().Be(0);
    }

    [Fact]
    public void Override_WithoutDeclaredEdge_Throws()
    {
        var policy = NewPolicy(new FakeEdges()); // no edges
        policy.Record(new RegistrationCandidate("command", "look", "pack-a", false, () => { }, "a.js", 1));
        policy.Record(new RegistrationCandidate("command", "look", "pack-b", true,  () => { }, "b.js", 1));

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("pack-b").And.Contain("dependency");
    }

    [Fact]
    public void Override_OfNonexistentBase_Throws()
    {
        var policy = NewPolicy(new FakeEdges().Edge("pack-b", "pack-a"));
        policy.Record(new RegistrationCandidate("command", "look", "pack-b", true, () => { }, "b.js", 1)); // no base
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("no base").And.Contain("look");
    }

    [Fact]
    public void TwoOverrides_OneBase_IsAmbiguous_Throws()
    {
        var edges = new FakeEdges().Edge("pack-b", "pack-a").Edge("pack-c", "pack-a");
        var policy = NewPolicy(edges);
        policy.Record(new RegistrationCandidate("command", "look", "pack-a", false, () => { }, "a.js", 1));
        policy.Record(new RegistrationCandidate("command", "look", "pack-b", true,  () => { }, "b.js", 1));
        policy.Record(new RegistrationCandidate("command", "look", "pack-c", true,  () => { }, "c.js", 1));
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Theory]
    [InlineData("kernel")]
    [InlineData("engine")]
    [InlineData("tapestry-core")]
    public void Override_OfKernelOwnedName_Throws_WithSpecificMessage(string kernelOwner)
    {
        var policy = NewPolicy(new FakeEdges().Edge("pack-b", kernelOwner));
        policy.Record(new RegistrationCandidate("tick", "heartbeat", kernelOwner, false, () => { }, "", 0));
        policy.Record(new RegistrationCandidate("tick", "heartbeat", "pack-b", true, () => { }, "b.js", 1));

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("not pack-overridable");
    }

    [Fact]
    public void Resolve_CalledTwice_Throws()
    {
        var policy = NewPolicy();
        policy.Record(new RegistrationCandidate("command", "look", "pack-a", false, () => { }, "a.js", 1));
        policy.Resolve();
        Assert.Throws<InvalidOperationException>(() => policy.Resolve());
    }

    [Fact]
    public void Override_OfOwnRegistration_Succeeds_WithoutEdge()
    {
        var policy = NewPolicy(new FakeEdges()); // no edges declared
        var baseFired = 0;
        var overrideFired = 0;
        policy.Record(new RegistrationCandidate("command", "look", "pack-a", false, () => baseFired++, "a.js", 1));
        policy.Record(new RegistrationCandidate("command", "look", "pack-a", true, () => overrideFired++, "a.js", 2));
        policy.Resolve();
        overrideFired.Should().Be(1);
        baseFired.Should().Be(0);
    }
}
