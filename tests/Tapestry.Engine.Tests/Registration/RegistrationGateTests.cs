using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Classes;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Registration;
using Xunit;

namespace Tapestry.Engine.Tests.Registration;

public class RegistrationGateTests
{
    // Edge oracle fake: edges is a set of "from->to" pairs (same shape as RegistrationPolicyTests).
    private sealed class FakeEdges : IPackEdgeOracle
    {
        private readonly HashSet<(string, string)> _edges = new();
        public FakeEdges Edge(string from, string to) { _edges.Add((from, to)); return this; }
        public bool DeclaresEdge(string from, string to) => _edges.Contains((from, to));
    }

    [Fact]
    public void ArmedGate_DirectRegistryWrite_IsABootError_TheMobsRegisterCommandShape()
    {
        // The exact v0.1.20 bypass that shipped #98: pack-land code writing straight into
        // CommandRegistry while pack loading is underway. The gate must make this throw.
        var gate = new RegistrationGate();
        var registry = new CommandRegistry(gate);
        gate.Arm();

        var act = () => registry.Register("say", _ => { }, roles: ["mob"]);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*bypasses RegistrationPolicy*");
    }

    [Fact]
    public void ArmedGate_WriteInsideCommitScope_Succeeds()
    {
        var gate = new RegistrationGate();
        var registry = new CommandRegistry(gate);
        gate.Arm();
        using (gate.EnterCommitScope())
        {
            registry.Register("say", _ => { });
        }
        registry.Resolve("say").Should().NotBeNull();
    }

    [Fact]
    public void DisarmedGate_DirectWrite_Succeeds()
    {
        var gate = new RegistrationGate();
        var registry = new CommandRegistry(gate);
        registry.Register("kernel-cmd", _ => { });
        registry.Resolve("kernel-cmd").Should().NotBeNull();
    }

    [Fact]
    public void PolicyCommit_RunsInsideCommitScope_EndToEnd()
    {
        // Construct the policy exactly as RegistrationPolicyTests does, but pass the gate:
        // pre-seal Record + Resolve() must not throw; the write lands.
        var gate = new RegistrationGate();
        var registry = new CommandRegistry(gate);
        var policy = new RegistrationPolicy(new FakeEdges(), gate);
        gate.Arm();

        policy.Record(new RegistrationCandidate(
            "command", "look", "pack-a", false,
            () => registry.Register("look", _ => { }, packName: "pack-a"),
            "scripts/a.js", 1));

        policy.Invoking(p => p.Resolve()).Should().NotThrow();
        registry.Resolve("look").Should().NotBeNull();
    }

    [Fact]
    public void PostSealRecord_AlsoRunsInsideCommitScope()
    {
        // D5 seam: post-seal Record resolves+commits immediately -- still scoped.
        var gate = new RegistrationGate();
        var registry = new CommandRegistry(gate);
        var policy = new RegistrationPolicy(new FakeEdges(), gate);
        gate.Arm();
        policy.Resolve(); // seal with an empty ledger

        var record = () => policy.Record(new RegistrationCandidate(
            "command", "lateverb", "pack-a", false,
            () => registry.Register("lateverb", _ => { }, packName: "pack-a"),
            "scripts/a.js", 1));

        record.Should().NotThrow();
        registry.Resolve("lateverb").Should().NotBeNull();
    }

    [Fact]
    public void ArmedGate_ClassRegistry_DirectWrite_Throws()
    {
        var gate = new RegistrationGate();
        var registry = new ClassRegistry(gate);
        gate.Arm();

        var act = () => registry.Register(new ClassDefinition { Id = "warrior", Name = "Warrior" });

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*bypasses RegistrationPolicy*");
    }

    [Fact]
    public void ArmedGate_EmoteRegistry_DirectWrite_Throws()
    {
        var gate = new RegistrationGate();
        var registry = new EmoteRegistry(gate);
        gate.Arm();

        var act = () => registry.Register(new EmoteDefinition
        {
            Name = "smile",
            SelfMessage = "You smile.",
            RoomMessage = "{actor} smiles."
        });

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*bypasses RegistrationPolicy*");
    }

    [Fact]
    public void ArmedGate_SlotRegistry_DirectWrite_Throws()
    {
        var gate = new RegistrationGate();
        var registry = new SlotRegistry(gate);
        gate.Arm();

        var act = () => registry.RegisterPackSlot("pack-a", "cloak", "Cloak", 1);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*bypasses RegistrationPolicy*");
    }

    [Fact]
    public void Di_InjectsGateIntoRegistries_OptionalParamFilledFromContainer()
    {
        // MS.DI must fill the optional trailing RegistrationGate? ctor params from the
        // container when the gate is registered -- prove it: arm the container's gate and
        // the container's CommandRegistry must enforce it.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        var provider = services.BuildServiceProvider();

        var gate = provider.GetRequiredService<RegistrationGate>();
        var registry = provider.GetRequiredService<CommandRegistry>();
        gate.Arm();

        var act = () => registry.Register("smuggled", _ => { });

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*bypasses RegistrationPolicy*");
    }
}
