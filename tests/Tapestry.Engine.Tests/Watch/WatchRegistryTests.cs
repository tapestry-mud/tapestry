using System;
using FluentAssertions;
using Tapestry.Engine.Watch;
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
        reg.Subscribe(target, "watcher-1", sink);
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
        reg.Subscribe(target, "watcher-1", sink);
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
        reg.Subscribe(t1, "w", sink);
        reg.Subscribe(t2, "w", sink);
        reg.GetSinks(t1).Should().BeEmpty();
        reg.GetSinks(t2).Should().ContainSingle();
    }
}
