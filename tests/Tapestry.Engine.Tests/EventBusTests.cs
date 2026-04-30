using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class EventBusTests
{
    [Fact]
    public void Subscribe_AndPublish_CallsHandler()
    {
        var bus = new EventBus();
        GameEvent? received = null;
        bus.Subscribe("test.event", (evt) => { received = evt; });
        var sent = new GameEvent { Type = "test.event" };
        bus.Publish(sent);
        received.Should().Be(sent);
    }

    [Fact]
    public void Publish_DoesNotCallUnrelatedHandlers()
    {
        var bus = new EventBus();
        var called = false;
        bus.Subscribe("other.event", (_) => { called = true; });
        bus.Publish(new GameEvent { Type = "test.event" });
        called.Should().BeFalse();
    }

    [Fact]
    public void MultipleHandlers_AllCalled_InPriorityOrder()
    {
        var bus = new EventBus();
        var order = new List<string>();
        bus.Subscribe("test.event", (_) => { order.Add("low"); }, priority: 10);
        bus.Subscribe("test.event", (_) => { order.Add("high"); }, priority: 100);
        bus.Subscribe("test.event", (_) => { order.Add("mid"); }, priority: 50);
        bus.Publish(new GameEvent { Type = "test.event" });
        order.Should().ContainInOrder("high", "mid", "low");
    }

    [Fact]
    public void CancelledEvent_StopsLaterHandlers()
    {
        var bus = new EventBus();
        var secondCalled = false;
        bus.Subscribe("test.event", (evt) => { evt.Cancelled = true; }, priority: 100);
        bus.Subscribe("test.event", (_) => { secondCalled = true; }, priority: 10);
        bus.Publish(new GameEvent { Type = "test.event" });
        secondCalled.Should().BeFalse();
    }

    [Fact]
    public void Unsubscribe_RemovesHandler()
    {
        var bus = new EventBus();
        var callCount = 0;
        var id = bus.Subscribe("test.event", (_) => { callCount++; });
        bus.Publish(new GameEvent { Type = "test.event" });
        callCount.Should().Be(1);
        bus.Unsubscribe(id);
        bus.Publish(new GameEvent { Type = "test.event" });
        callCount.Should().Be(1);
    }

    [Fact]
    public void WildcardSubscription_MatchesAll()
    {
        var bus = new EventBus();
        var received = new List<string>();
        bus.Subscribe("*", (evt) => { received.Add(evt.Type); });
        bus.Publish(new GameEvent { Type = "entity.damaged" });
        bus.Publish(new GameEvent { Type = "room.entered" });
        received.Should().HaveCount(2);
    }
}
