using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Registration;
using Tapestry.Scripting.Interop;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class EquipmentSlotRegistrationTests
{
    private static (JintRuntime rt, RegistrationPolicy policy, SlotRegistry slots, PackDependencyGraph graph)
        BuildRuntime(Dictionary<string, List<string>>? deps = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<PackDependencyGraph>();
        graph.Build(deps ?? new Dictionary<string, List<string>>());
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<RegistrationPolicy>(),
                provider.GetRequiredService<SlotRegistry>(), graph);
    }

    private const string CloakSlot =
        "tapestry.equipment.registerSlot({ name: 'cloak', display: 'Cloak', max: 1 });";

    [Fact]
    public void Slot_IsDeferred_UntilSeal()
    {
        var (rt, policy, slots, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", CloakSlot, "scripts/a.js");
        slots.GetSlot("cloak").Should().BeNull("slots must commit at the seal, not eagerly");
        policy.Resolve();
        slots.GetSlot("cloak").Should().NotBeNull();
    }

    [Fact]
    public void Slot_IsPackOwned_NotEngineOwned()
    {
        var (rt, policy, slots, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", CloakSlot, "scripts/a.js");
        policy.Resolve();
        slots.GetSlot("cloak")!.Scope.Should().Be("pack-a",
            "JS-registered slots are pack-owned; 'engine' is reserved for C# boot code");
    }

    [Fact]
    public void TwoPacks_SameSlot_NoOverride_BootError()
    {
        var (rt, policy, _, _) = BuildRuntime();
        EsmTest.Load(rt, "pack-a", CloakSlot, "scripts/a.js");
        EsmTest.Load(rt, "pack-b", CloakSlot, "scripts/b.js");
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Resolve());
        ex.Message.Should().Contain("cloak").And.Contain("override");
    }

    [Fact]
    public void Override_WithDeclaredEdge_Wins()
    {
        var (rt, policy, slots, _) = BuildRuntime(new() { ["pack-b"] = new() { "pack-a" } });
        EsmTest.Load(rt, "pack-a", CloakSlot, "scripts/a.js");
        EsmTest.Load(rt, "pack-b",
            "tapestry.equipment.registerSlot({ name: 'cloak', display: 'Mantle', max: 2, override: true });",
            "scripts/b.js");
        policy.Resolve();
        var slot = slots.GetSlot("cloak");
        slot.Should().NotBeNull();
        slot!.Display.Should().Be("Mantle");
        slot.Max.Should().Be(2);
    }
}
