using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class EntityStatusBroadcasterTests
{
    [Fact]
    public void ObservableProperty_Change_PublishesStatusChanged()
    {
        var bus = new EventBus();
        var registry = new PropertyRegistry();
        registry.RegisterObservable("sustenance", "status");
        var broadcaster = new EntityStatusBroadcaster(registry, bus);

        var entity = new Entity("player", "Eater");
        entity.RegisterPropertyObserver(broadcaster);

        var seen = new List<GameEvent>();
        bus.Subscribe("entity.status.changed", seen.Add);

        entity.SetProperty("sustenance", 0);

        seen.Should().ContainSingle();
        seen[0].SourceEntityId.Should().Be(entity.Id);
        seen[0].Data["key"].Should().Be("sustenance");
        seen[0].Data["new"].Should().Be(0);
    }

    [Fact]
    public void NonObservableProperty_Change_DoesNotPublish()
    {
        var bus = new EventBus();
        var registry = new PropertyRegistry();
        var broadcaster = new EntityStatusBroadcaster(registry, bus);
        var entity = new Entity("player", "X");
        entity.RegisterPropertyObserver(broadcaster);
        var seen = new List<GameEvent>();
        bus.Subscribe("*", seen.Add);

        entity.SetProperty("ai_scratch", 7);

        seen.Should().BeEmpty();
    }

    [Fact]
    public void SameValue_DoesNotNotify()
    {
        var bus = new EventBus();
        var registry = new PropertyRegistry();
        registry.RegisterObservable("sustenance", "status");
        var broadcaster = new EntityStatusBroadcaster(registry, bus);
        var entity = new Entity("player", "X");
        entity.SetProperty("sustenance", 50);
        entity.RegisterPropertyObserver(broadcaster);
        var seen = new List<GameEvent>();
        bus.Subscribe("entity.status.changed", seen.Add);

        entity.SetProperty("sustenance", 50);

        seen.Should().BeEmpty();
    }
}
