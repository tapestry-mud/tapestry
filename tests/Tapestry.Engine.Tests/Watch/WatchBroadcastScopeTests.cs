using FluentAssertions;
using Tapestry.Engine.Watch;

namespace Tapestry.Engine.Tests.Watch;

public class WatchBroadcastScopeTests
{
    [Fact]
    public void Suppressed_DefaultsFalse_AndScopeRestoresPriorState()
    {
        WatchBroadcastScope.Suppressed.Should().BeFalse();

        WatchBroadcastScope.Run(() => WatchBroadcastScope.Suppressed.Should().BeTrue());

        WatchBroadcastScope.Suppressed.Should().BeFalse();
    }

    [Fact]
    public void Tee_UnderSuppressedScope_DeliversToPlayerButNotWatchers()
    {
        var reg = new WatchRegistry();
        var owner = Guid.NewGuid();
        var inner = new FakeConnection();
        var sink = new FakeConnection();
        var tee = new TeeConnection(inner, reg, () => owner);
        reg.Subscribe(owner, "w", () => sink);

        WatchBroadcastScope.Run(() => tee.SendLine("a private tell"));

        inner.SentLines.Should().ContainSingle().Which.Should().Be("a private tell\r\n"); // player sees it
        sink.SentLines.Should().BeEmpty(); // watcher does NOT
    }

    [Fact]
    public void Tee_OutsideSuppressedScope_StillBroadcastsNormally()
    {
        var reg = new WatchRegistry();
        var owner = Guid.NewGuid();
        var inner = new FakeConnection();
        var sink = new FakeConnection();
        var tee = new TeeConnection(inner, reg, () => owner);
        reg.Subscribe(owner, "w", () => sink);

        tee.SendLine("public room output");

        inner.SentLines.Should().ContainSingle();
        sink.SentLines.Should().ContainSingle().Which.Should().Be("public room output\r\n");
    }
}
