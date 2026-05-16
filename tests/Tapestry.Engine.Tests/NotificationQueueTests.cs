using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class NotificationQueueTests
{
    [Fact]
    public void DrainFor_ReturnsEnqueuedNotifications_InInsertionOrder()
    {
        var queue = new NotificationQueue();
        var entityId = Guid.NewGuid();

        queue.Enqueue(entityId, new Notification("quest_complete", 50, "Quest done!\r\n"));
        queue.Enqueue(entityId, new Notification("achievement", 50, "Achievement unlocked!\r\n"));

        var result = queue.DrainFor(entityId);

        result.Should().HaveCount(2);
        result[0].Type.Should().Be("quest_complete");
        result[1].Type.Should().Be("achievement");
    }

    [Fact]
    public void DrainFor_EmptiesQueue_SecondCallReturnsEmpty()
    {
        var queue = new NotificationQueue();
        var entityId = Guid.NewGuid();

        queue.Enqueue(entityId, new Notification("quest_complete", 50, "Done!\r\n"));
        queue.DrainFor(entityId);
        var second = queue.DrainFor(entityId);

        second.Should().BeEmpty();
    }

    [Fact]
    public void DrainFor_UnknownEntity_ReturnsEmptyList()
    {
        var queue = new NotificationQueue();
        var result = queue.DrainFor(Guid.NewGuid());
        result.Should().BeEmpty();
    }

    [Fact]
    public void DrainFor_SortsByPriority_LowerFirst()
    {
        var queue = new NotificationQueue();
        var entityId = Guid.NewGuid();

        queue.Enqueue(entityId, new Notification("low_priority", 50, "Low.\r\n"));
        queue.Enqueue(entityId, new Notification("high_priority", 25, "High.\r\n"));

        var result = queue.DrainFor(entityId);

        result[0].Type.Should().Be("high_priority");
        result[1].Type.Should().Be("low_priority");
    }

    [Fact]
    public void DrainFor_SamePriority_PreservesInsertionOrder()
    {
        var queue = new NotificationQueue();
        var entityId = Guid.NewGuid();

        queue.Enqueue(entityId, new Notification("first", 50, "1\r\n"));
        queue.Enqueue(entityId, new Notification("second", 50, "2\r\n"));
        queue.Enqueue(entityId, new Notification("third", 50, "3\r\n"));

        var result = queue.DrainFor(entityId);

        result.Select(n => n.Type).Should().ContainInOrder("first", "second", "third");
    }

    [Fact]
    public void DrainFor_SeparateEntities_IndependentQueues()
    {
        var queue = new NotificationQueue();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        queue.Enqueue(alice, new Notification("alice_quest", 50, "Alice done!\r\n"));
        queue.Enqueue(bob, new Notification("bob_quest", 50, "Bob done!\r\n"));

        var aliceResult = queue.DrainFor(alice);
        var bobResult = queue.DrainFor(bob);

        aliceResult.Should().ContainSingle(n => n.Type == "alice_quest");
        bobResult.Should().ContainSingle(n => n.Type == "bob_quest");
    }
}
