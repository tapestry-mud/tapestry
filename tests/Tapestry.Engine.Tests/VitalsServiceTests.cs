using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Stats;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class VitalsServiceTests
{
    private static (VitalsService svc, EventBus bus, Entity entity, List<GameEvent> seen) Build()
    {
        var bus = new EventBus();
        var svc = new VitalsService(bus);
        var entity = new Entity("player", "Tester");
        entity.Stats.BaseMaxHp = 100;
        entity.Stats.InitializeVitals(80, 0, 0);
        var seen = new List<GameEvent>();
        bus.Subscribe("entity.vital.changed", seen.Add);
        return (svc, bus, entity, seen);
    }

    [Fact]
    public void Apply_PublishesVitalChanged_WithDeltaAndReason()
    {
        var (svc, _, entity, seen) = Build();

        var result = svc.Apply(entity, VitalKind.Hp, -30, "combat.melee");

        result.Should().Be(50);
        entity.Stats.Hp.Should().Be(50);
        seen.Should().ContainSingle();
        var evt = seen[0];
        evt.SourceEntityId.Should().Be(entity.Id);
        evt.Data["vital"].Should().Be("hp");
        evt.Data["old"].Should().Be(80);
        evt.Data["new"].Should().Be(50);
        evt.Data["delta"].Should().Be(-30);
        evt.Data["reason"].Should().Be("combat.melee");
    }

    [Fact]
    public void Apply_NoOp_DoesNotPublish()
    {
        var (svc, _, entity, seen) = Build();

        svc.Apply(entity, VitalKind.Hp, 0, "combat.melee");

        seen.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ClampedToFloor_PublishesActualClampedDelta()
    {
        var (svc, _, entity, seen) = Build();

        var result = svc.Apply(entity, VitalKind.Hp, -999, "combat.melee");

        result.Should().Be(0);
        seen.Should().ContainSingle();
        seen[0].Data["new"].Should().Be(0);
        seen[0].Data["delta"].Should().Be(-80);
    }

    [Fact]
    public void Set_ClampedToMax_PublishesWhenChanged()
    {
        var (svc, _, entity, seen) = Build();

        var result = svc.Set(entity, VitalKind.Hp, 500, "admin");

        result.Should().Be(100);
        seen.Should().ContainSingle();
        seen[0].Data["new"].Should().Be(100);
    }

    [Fact]
    public void RestoreToMax_PublishesPerChangedVital()
    {
        var (svc, _, entity, seen) = Build();
        entity.Stats.BaseMaxResource = 50;
        entity.Stats.BaseMaxMovement = 80;
        entity.Stats.InitializeVitals(80, 10, 80); // hp below max, resource below max, movement at max

        svc.RestoreToMax(entity, "script.restore");

        // hp 80->100 and resource 10->50 publish; movement 80->80 is a no-op.
        seen.Select(e => (string)e.Data["vital"]!).Should().BeEquivalentTo(new[] { "hp", "resource" });
        entity.Stats.Hp.Should().Be(100);
        entity.Stats.Resource.Should().Be(50);
    }

    [Fact]
    public void Initialize_DoesNotPublish()
    {
        var (svc, _, entity, seen) = Build();

        svc.Initialize(entity, 10, 20, 30);

        seen.Should().BeEmpty();
        entity.Stats.Hp.Should().Be(10);
    }
}
