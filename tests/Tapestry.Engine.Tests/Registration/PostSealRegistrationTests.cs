using System;
using System.Collections.Generic;
using FluentAssertions;
using Tapestry.Engine.Registration;
using Xunit;

namespace Tapestry.Engine.Tests.Registration;

public class PostSealRegistrationTests
{
    private sealed class StubEdges : IPackEdgeOracle
    {
        private readonly HashSet<(string, string)> _edges = new();
        public StubEdges Edge(string from, string to) { _edges.Add((from, to)); return this; }
        public bool DeclaresEdge(string fromPack, string toPack) => _edges.Contains((fromPack, toPack));
    }

    private static RegistrationCandidate Cand(
        string kind, string name, string owner, bool isOverride, Action commit) =>
        new(kind, name, owner, isOverride, commit, SourceFile: "", Line: 0);

    [Fact]
    public void PostSeal_NewName_CommitsImmediately()
    {
        var policy = new RegistrationPolicy(new StubEdges());
        policy.Resolve();

        var committed = false;
        policy.Record(Cand("export", "pack-a:late", "pack-a", isOverride: false, () => committed = true));

        committed.Should().BeTrue();
    }

    [Fact]
    public void PostSeal_DuplicateOfSealedWinner_ThrowsAndDoesNotCommit()
    {
        var policy = new RegistrationPolicy(new StubEdges());
        policy.Record(Cand("tick", "pulse", "pack-a", isOverride: false, () => { }));
        policy.Resolve();

        var committed = false;
        var act = () => policy.Record(Cand("tick", "pulse", "pack-b", isOverride: false, () => committed = true));

        act.Should().Throw<InvalidOperationException>().WithMessage("*pulse*");
        committed.Should().BeFalse();
    }

    [Fact]
    public void PostSeal_OverrideWithEdge_CommitsImmediately()
    {
        var edges = new StubEdges().Edge("pack-b", "pack-a");
        var policy = new RegistrationPolicy(edges);
        policy.Record(Cand("command", "brew", "pack-a", isOverride: false, () => { }));
        policy.Resolve();

        var committed = false;
        policy.Record(Cand("command", "brew", "pack-b", isOverride: true, () => committed = true));

        committed.Should().BeTrue();
    }

    [Fact]
    public void PostSeal_OverrideWithoutEdge_Throws()
    {
        var policy = new RegistrationPolicy(new StubEdges());
        policy.Record(Cand("command", "brew", "pack-a", isOverride: false, () => { }));
        policy.Resolve();

        var act = () => policy.Record(Cand("command", "brew", "pack-b", isOverride: true, () => { }));

        act.Should().Throw<InvalidOperationException>().WithMessage("*dependency*");
    }

    [Fact]
    public void PostSeal_OverrideOfKernelOwned_ThrowsKernelDiagnostic()
    {
        var policy = new RegistrationPolicy(new StubEdges());
        policy.Record(Cand("tick", "heartbeat", "kernel", isOverride: false, () => { }));
        policy.Resolve();

        var act = () => policy.Record(Cand("tick", "heartbeat", "pack-a", isOverride: true, () => { }));

        act.Should().Throw<InvalidOperationException>().WithMessage("*not pack-overridable*");
    }

    [Fact]
    public void PostSeal_SecondRegistrationOfSamePostSealName_Throws()
    {
        var policy = new RegistrationPolicy(new StubEdges());
        policy.Resolve();
        policy.Record(Cand("export", "pack-a:late", "pack-a", isOverride: false, () => { }));

        var act = () => policy.Record(Cand("export", "pack-a:late", "pack-a", isOverride: false, () => { }));

        act.Should().Throw<InvalidOperationException>();
    }
}
