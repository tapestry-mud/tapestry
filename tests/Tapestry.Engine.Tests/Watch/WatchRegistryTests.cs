using System;
using FluentAssertions;
using Tapestry.Engine.Watch;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests.Watch;

public class WatchRegistryTests
{
    [Fact]
    public void Subscribe_ThenGetSinks_ReturnsSink()
    {
        var reg = new WatchRegistry();
        var target = Guid.NewGuid();
        var sink = new FakeConnection();
        reg.Subscribe(target, "watcher-1", () => sink);
        reg.GetSinks(target).Should().ContainSingle().Which.Should().BeSameAs(sink);
    }

    [Fact]
    public void GetSinks_UnknownTarget_IsEmpty()
    {
        var reg = new WatchRegistry();
        reg.GetSinks(Guid.NewGuid()).Should().BeEmpty();
    }

    [Fact]
    public void Unsubscribe_RemovesSink()
    {
        var reg = new WatchRegistry();
        var target = Guid.NewGuid();
        var sink = new FakeConnection();
        reg.Subscribe(target, "watcher-1", () => sink);
        reg.Unsubscribe("watcher-1").Should().BeTrue();
        reg.GetSinks(target).Should().BeEmpty();
    }

    [Fact]
    public void Unsubscribe_UnknownWatcher_ReturnsFalse()
    {
        var reg = new WatchRegistry();
        reg.Unsubscribe("nope").Should().BeFalse();
    }

    [Fact]
    public void Subscribe_SameWatcherNewTarget_MovesWatcher()
    {
        var reg = new WatchRegistry();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var sink = new FakeConnection();
        reg.Subscribe(t1, "w", () => sink);
        reg.Subscribe(t2, "w", () => sink);
        reg.GetSinks(t1).Should().BeEmpty();
        reg.GetSinks(t2).Should().ContainSingle();
    }

    /// <summary>
    /// After subscribing with a resolver that closes over a mutable holder, swapping the
    /// holder's value makes GetSinks return the NEW connection — proving reconnect-safety.
    /// </summary>
    [Fact]
    public void GetSinks_ResolverSwapped_ReturnsNewConnection()
    {
        var reg = new WatchRegistry();
        var target = Guid.NewGuid();
        var original = new FakeConnection();
        var replacement = new FakeConnection();

        IConnection? current = original;
        reg.Subscribe(target, "watcher-1", () => current);

        reg.GetSinks(target).Should().ContainSingle().Which.Should().BeSameAs(original);

        // Simulate a reconnect: the holder now points to the new connection.
        current = replacement;

        reg.GetSinks(target).Should().ContainSingle().Which.Should().BeSameAs(replacement);
    }

    /// <summary>
    /// RemoveTarget removes the target's sinks entry. Any watcher key that was registered
    /// under that target is also removed from the reverse index, so a subsequent Unsubscribe
    /// returns false and GetSinks is empty.
    /// </summary>
    [Fact]
    public void RemoveTarget_ClearsTargetAndWatcherKeys()
    {
        var reg = new WatchRegistry();
        var target = Guid.NewGuid();
        var sink = new FakeConnection();
        reg.Subscribe(target, "watcher-1", () => sink);

        reg.RemoveTarget(target);

        reg.GetSinks(target).Should().BeEmpty();
        // The watcher key was also evicted from the reverse index.
        reg.Unsubscribe("watcher-1").Should().BeFalse();
    }

    [Fact]
    public void GetSinks_NullResolver_ExcludesNullResult()
    {
        var reg = new WatchRegistry();
        var target = Guid.NewGuid();
        // Resolver returns null (e.g. watcher has logged off but not yet unsubscribed).
        reg.Subscribe(target, "watcher-1", () => null);
        reg.GetSinks(target).Should().BeEmpty();
    }
}
