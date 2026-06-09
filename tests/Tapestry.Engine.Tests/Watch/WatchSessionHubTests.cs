using FluentAssertions;
using Tapestry.Engine.Watch;

namespace Tapestry.Engine.Tests.Watch;

public class WatchSessionHubTests
{
    private sealed class EmptyRoster : IWatchRosterSource
    {
        public IReadOnlyList<WatchRosterEntry> Snapshot() => Array.Empty<WatchRosterEntry>();
    }

    private static WatchSession NewSession(FakeWatchSink sink) =>
        new(sink, new WatchRegistry(), new EmptyRoster());

    [Fact]
    public void RegisterUnregister_TracksCount()
    {
        var hub = new WatchSessionHub();
        var session = NewSession(new FakeWatchSink());

        hub.Register(session);
        hub.Count.Should().Be(1);

        hub.Unregister(session.Id);
        hub.Count.Should().Be(0);
    }

    [Fact]
    public void Broadcast_SendsRosterToEveryWatcher()
    {
        var hub = new WatchSessionHub();
        var sinkA = new FakeWatchSink();
        var sinkB = new FakeWatchSink();
        hub.Register(NewSession(sinkA));
        hub.Register(NewSession(sinkB));

        hub.Broadcast(new List<WatchRosterEntry> { new("id", "Alpha", "") });

        sinkA.Rosters.Should().ContainSingle();
        sinkB.Rosters.Should().ContainSingle();
    }
}
