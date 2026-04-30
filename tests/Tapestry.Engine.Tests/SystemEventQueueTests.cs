using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class SystemEventQueueTests
{
    [Fact]
    public void DisconnectEvent_stores_session_and_reason()
    {
        var sessionId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var evt = new DisconnectEvent(sessionId, entityId, "connection closed");

        evt.SessionId.Should().Be(sessionId);
        evt.EntityId.Should().Be(entityId);
        evt.Reason.Should().Be("connection closed");
        evt.Should().BeAssignableTo<SystemEvent>();
    }

    [Fact]
    public void ConnectEvent_stores_session_entity_and_room()
    {
        var sessionId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var evt = new ConnectEvent(sessionId, entityId, "town-square");

        evt.SessionId.Should().Be(sessionId);
        evt.EntityId.Should().Be(entityId);
        evt.SpawnRoomId.Should().Be("town-square");
        evt.Should().BeAssignableTo<SystemEvent>();
    }

    [Fact]
    public void Enqueue_and_DrainAll_returns_events_in_order()
    {
        var queue = new SystemEventQueue();
        var evt1 = new DisconnectEvent(Guid.NewGuid(), Guid.NewGuid(), "reason1");
        var evt2 = new DisconnectEvent(Guid.NewGuid(), Guid.NewGuid(), "reason2");

        queue.Enqueue(evt1);
        queue.Enqueue(evt2);

        var drained = queue.DrainAll();
        drained.Should().HaveCount(2);
        drained[0].Should().BeSameAs(evt1);
        drained[1].Should().BeSameAs(evt2);
    }

    [Fact]
    public void DrainAll_returns_empty_list_when_no_events()
    {
        var queue = new SystemEventQueue();

        var drained = queue.DrainAll();

        drained.Should().BeEmpty();
    }

    [Fact]
    public void DrainAll_clears_the_queue()
    {
        var queue = new SystemEventQueue();
        queue.Enqueue(new DisconnectEvent(Guid.NewGuid(), Guid.NewGuid(), "reason"));

        queue.DrainAll();
        var second = queue.DrainAll();

        second.Should().BeEmpty();
    }

    [Fact]
    public void Count_reflects_current_queue_depth()
    {
        var queue = new SystemEventQueue();
        queue.Count.Should().Be(0);

        queue.Enqueue(new DisconnectEvent(Guid.NewGuid(), Guid.NewGuid(), "reason"));
        queue.Count.Should().Be(1);

        queue.DrainAll();
        queue.Count.Should().Be(0);
    }
}
