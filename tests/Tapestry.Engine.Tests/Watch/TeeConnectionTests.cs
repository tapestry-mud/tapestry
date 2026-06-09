using System;
using FluentAssertions;
using Tapestry.Engine.Watch;
using Xunit;

namespace Tapestry.Engine.Tests.Watch;

public class TeeConnectionTests
{
    [Fact]
    public void SendLine_ForwardsToInner()
    {
        var inner = new FakeConnection();
        var tee = new TeeConnection(inner, new WatchRegistry(), () => Guid.NewGuid());
        tee.SendLine("hello");
        inner.SentLines.Should().ContainSingle().Which.Should().Be("hello\r\n");
    }

    [Fact]
    public void SendLine_WithWatcher_DeliversToBothInnerAndSink()
    {
        var inner = new FakeConnection();
        var sink = new FakeConnection();
        var reg = new WatchRegistry();
        var owner = Guid.NewGuid();
        reg.Subscribe(owner, "w", sink);
        var tee = new TeeConnection(inner, reg, () => owner);
        tee.SendLine("watch me");
        inner.SentLines.Should().ContainSingle().Which.Should().Be("watch me\r\n");
        sink.SentLines.Should().ContainSingle().Which.Should().Be("watch me\r\n");
    }

    [Fact]
    public void SendText_WithWatcher_DeliversToSink()
    {
        var inner = new FakeConnection();
        var sink = new FakeConnection();
        var reg = new WatchRegistry();
        var owner = Guid.NewGuid();
        reg.Subscribe(owner, "w", sink);
        var tee = new TeeConnection(inner, reg, () => owner);
        tee.SendText("frag");
        sink.SentText.Should().ContainSingle().Which.Should().Be("frag");
    }

    [Fact]
    public void ShouldBroadcastFalse_SuppressesWatchers_ButNotInner()
    {
        var inner = new FakeConnection();
        var sink = new FakeConnection();
        var reg = new WatchRegistry();
        var owner = Guid.NewGuid();
        reg.Subscribe(owner, "w", sink);
        var tee = new TeeConnection(inner, reg, () => owner) { ShouldBroadcast = false };
        tee.SendLine("secret");
        inner.SentLines.Should().ContainSingle();
        sink.SentLines.Should().BeEmpty();
    }

    [Fact]
    public void NullOwner_NoBroadcast_StillForwardsToInner()
    {
        var inner = new FakeConnection();
        var sink = new FakeConnection();
        var reg = new WatchRegistry();
        var tee = new TeeConnection(inner, reg, () => (Guid?)null);
        tee.SendLine("x");
        inner.SentLines.Should().ContainSingle();
        sink.SentLines.Should().BeEmpty();
    }

    [Fact]
    public void Delegates_IdAndIsConnected_ToInner()
    {
        var inner = new FakeConnection("conn-42");
        var tee = new TeeConnection(inner, new WatchRegistry(), () => (Guid?)null);
        tee.Id.Should().Be("conn-42");
        tee.IsConnected.Should().BeTrue();
    }
}
