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

    [Fact]
    public void Publish_Twice_SameType_BothCallsFireHandler()
    {
        var bus = new EventBus();
        var callCount = 0;
        bus.Subscribe("test.cache", (_) => { callCount++; });

        bus.Publish(new GameEvent { Type = "test.cache" });
        bus.Publish(new GameEvent { Type = "test.cache" });

        callCount.Should().Be(2);
    }

    [Fact]
    public void Subscribe_AfterPublish_NewHandlerCalledOnNextPublish()
    {
        var bus = new EventBus();
        var firstCount = 0;
        var secondCount = 0;
        bus.Subscribe("cache.invalidate", (_) => { firstCount++; });
        bus.Publish(new GameEvent { Type = "cache.invalidate" });

        bus.Subscribe("cache.invalidate", (_) => { secondCount++; });
        bus.Publish(new GameEvent { Type = "cache.invalidate" });

        firstCount.Should().Be(2);
        secondCount.Should().Be(1);
    }

    [Fact]
    public void Unsubscribe_AfterPublish_RemovedHandlerNotCalledOnNextPublish()
    {
        var bus = new EventBus();
        var callCount = 0;
        var id = bus.Subscribe("cache.unsub", (_) => { callCount++; });
        bus.Publish(new GameEvent { Type = "cache.unsub" });

        bus.Unsubscribe(id);
        bus.Publish(new GameEvent { Type = "cache.unsub" });

        callCount.Should().Be(1);
    }

    [Fact]
    public void WildcardSubscribeAfterPublish_InvalidatesAllCachedTypes()
    {
        var bus = new EventBus();
        var specificCount = 0;
        var wildcardCount = 0;
        bus.Subscribe("specific.event", (_) => { specificCount++; });
        bus.Publish(new GameEvent { Type = "specific.event" });

        bus.Subscribe("*", (_) => { wildcardCount++; });
        bus.Publish(new GameEvent { Type = "specific.event" });

        specificCount.Should().Be(2);
        wildcardCount.Should().Be(1);
    }

    [Fact]
    public void Publish_FirstHandlerThrows_SecondHandlerStillFires()
    {
        var bus = new EventBus();
        var secondFired = false;

        bus.Subscribe("test.event", (_) =>
        {
            throw new InvalidOperationException("handler boom");
        }, priority: 100);
        bus.Subscribe("test.event", (_) =>
        {
            secondFired = true;
        }, priority: 10);

        bus.Publish(new GameEvent { Type = "test.event" });

        secondFired.Should().BeTrue();
    }

    [Fact]
    public void Publish_FirstHandlerThrows_CancelledEventStillStops()
    {
        var bus = new EventBus();
        var secondFired = false;

        bus.Subscribe("test.event", (evt) =>
        {
            evt.Cancelled = true;
            throw new InvalidOperationException("cancel then throw");
        }, priority: 100);
        bus.Subscribe("test.event", (_) =>
        {
            secondFired = true;
        }, priority: 10);

        bus.Publish(new GameEvent { Type = "test.event" });

        secondFired.Should().BeFalse();
    }

    [Fact]
    public void NestedPublish_InFlightIterationNotCorrupted()
    {
        var bus = new EventBus();
        var outerCount = 0;
        var innerCount = 0;
        bus.Subscribe("outer", (evt) =>
        {
            outerCount++;
            bus.Publish(new GameEvent { Type = "inner" });
            innerCount++;
        });
        bus.Subscribe("inner", (_) => { });

        bus.Publish(new GameEvent { Type = "outer" });

        outerCount.Should().Be(1);
        innerCount.Should().Be(1);
    }
}
