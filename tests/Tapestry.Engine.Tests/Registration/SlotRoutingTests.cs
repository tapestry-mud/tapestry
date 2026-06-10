using FluentAssertions;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Registration;

namespace Tapestry.Engine.Tests.Registration;

/// <summary>
/// Equipment slots route through RegistrationPolicy as kind "slot": collisions are located
/// boot errors, core's slots are pack-owned (edge-overridable), and only genuine engine C#
/// registrations (owner "engine") refuse override.
/// </summary>
public class SlotRoutingTests
{
    private sealed class FakeEdges : IPackEdgeOracle
    {
        private readonly HashSet<(string, string)> _edges = new();
        public FakeEdges Edge(string from, string to) { _edges.Add((from, to)); return this; }
        public bool DeclaresEdge(string from, string to) => _edges.Contains((from, to));
    }

    private static RegistrationCandidate SlotCandidate(
        SlotRegistry registry, string owner, string name, string display, int max,
        bool isOverride, string sourceFile)
        => new(
            Kind: "slot",
            Name: name,
            Owner: owner,
            IsOverride: isOverride,
            Commit: () => registry.RegisterPackSlot(owner, name, display, max),
            SourceFile: sourceFile,
            Line: 0);

    [Fact]
    public void TwoPacks_SameSlot_NoOverride_BootError_NamesBothPacks()
    {
        var registry = new SlotRegistry();
        var policy = new RegistrationPolicy(new FakeEdges());
        policy.Record(SlotCandidate(registry, "pack-a", "head", "Head", 1, false, "packs/pack-a/equipment_slots.yaml"));
        policy.Record(SlotCandidate(registry, "pack-b", "head", "Helm", 1, false, "packs/pack-b/equipment_slots.yaml"));

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("head")
            .And.Contain("pack-a")
            .And.Contain("pack-b")
            .And.Contain("equipment_slots.yaml");
    }

    [Fact]
    public void Override_WithDeclaredEdge_WinnerDisplayLands()
    {
        var registry = new SlotRegistry();
        var policy = new RegistrationPolicy(new FakeEdges().Edge("pack-b", "pack-a"));
        policy.Record(SlotCandidate(registry, "pack-a", "head", "Head", 1, false, "packs/pack-a/equipment_slots.yaml"));
        policy.Record(SlotCandidate(registry, "pack-b", "head", "Helm", 1, true, "packs/pack-b/equipment_slots.yaml"));

        policy.Resolve();

        registry.GetSlot("head")!.Display.Should().Be("Helm");
        registry.AllSlots.Should().ContainSingle(s => s.Name == "head");
    }

    [Fact]
    public void CoreSlots_ArePackOwned_OverridableViaEdge()
    {
        // The locked contract: @tapestry/core CONTENT is overridable via the dependency
        // edge -- core's equipment_slots.yaml must NOT register as owner "engine".
        var registry = new SlotRegistry();
        var policy = new RegistrationPolicy(new FakeEdges().Edge("my-world", "tapestry-core"));
        policy.Record(SlotCandidate(registry, "tapestry-core", "wield", "<wielded>", 1, false, "packs/core/equipment_slots.yaml"));
        policy.Record(SlotCandidate(registry, "my-world", "wield", "<main hand>", 1, true, "packs/my-world/equipment_slots.yaml"));

        policy.Resolve();

        registry.GetSlot("wield")!.Display.Should().Be("<main hand>");
    }

    [Fact]
    public void EngineOwnedSlot_RefusesOverride()
    {
        var registry = new SlotRegistry();
        var policy = new RegistrationPolicy(new FakeEdges().Edge("my-world", "engine"));
        policy.Record(new RegistrationCandidate(
            Kind: "slot",
            Name: "wield",
            Owner: "engine",
            IsOverride: false,
            Commit: () => registry.RegisterEngineSlot("wield", "<wielded>", 1),
            SourceFile: "",
            Line: 0));
        policy.Record(SlotCandidate(registry, "my-world", "wield", "<main hand>", 1, true, "packs/my-world/equipment_slots.yaml"));

        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("not pack-overridable");
    }
}
