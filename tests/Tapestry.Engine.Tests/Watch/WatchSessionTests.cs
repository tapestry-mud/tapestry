using FluentAssertions;
using Tapestry.Engine.Watch;

namespace Tapestry.Engine.Tests.Watch;

public class WatchSessionTests
{
    private sealed class StubRoster : IWatchRosterSource
    {
        public List<WatchRosterEntry> Entries { get; } = new();
        public IReadOnlyList<WatchRosterEntry> Snapshot() => Entries.ToList();
    }

    private static (WatchSession session, FakeWatchSink sink, WatchRegistry registry, StubRoster roster) Build()
    {
        var sink = new FakeWatchSink();
        var registry = new WatchRegistry();
        var roster = new StubRoster();
        var session = new WatchSession(sink, registry, roster);
        return (session, sink, registry, roster);
    }

    [Fact]
    public void Watch_KnownTarget_SubscribesAndSendsStatusAndSgrReset()
    {
        var (session, sink, registry, roster) = Build();
        var target = Guid.NewGuid();
        roster.Entries.Add(new WatchRosterEntry(target.ToString(), "Mallek", "lf:square"));

        sink.SimulateInput("watch " + target);

        registry.GetSinks(target).Should().ContainSingle().Which.Should().BeSameAs(sink);
        session.CurrentTarget.Should().Be(target.ToString());
        sink.Statuses.Should().Contain(s => s.Contains("Mallek"));
        sink.WatchOutputs.Should().Contain("\x1b[0m");
    }

    [Fact]
    public void Watch_UnparseableId_DoesNotSubscribe()
    {
        var (session, sink, _, _) = Build();

        sink.SimulateInput("watch not-a-guid");

        session.CurrentTarget.Should().BeNull();
        sink.Statuses.Should().Contain("Unknown stream.");
    }

    [Fact]
    public void Watch_OfflineTarget_DoesNotSubscribe()
    {
        var (session, sink, registry, _) = Build();
        var target = Guid.NewGuid();

        sink.SimulateInput("watch " + target);

        registry.GetSinks(target).Should().BeEmpty();
        session.CurrentTarget.Should().BeNull();
        sink.Statuses.Should().Contain(s => s.Contains("not available"));
    }

    [Fact]
    public void Unwatch_RemovesSubscription()
    {
        var (session, sink, registry, roster) = Build();
        var target = Guid.NewGuid();
        roster.Entries.Add(new WatchRosterEntry(target.ToString(), "Mallek", ""));
        sink.SimulateInput("watch " + target);

        sink.SimulateInput("unwatch");

        registry.GetSinks(target).Should().BeEmpty();
        session.CurrentTarget.Should().BeNull();
        sink.Statuses.Should().Contain("Stopped watching.");
    }

    [Fact]
    public void Disconnect_RemovesSubscription()
    {
        var (_, sink, registry, roster) = Build();
        var target = Guid.NewGuid();
        roster.Entries.Add(new WatchRosterEntry(target.ToString(), "Mallek", ""));
        sink.SimulateInput("watch " + target);

        sink.Disconnect("client closed");

        registry.GetSinks(target).Should().BeEmpty();
    }

    [Fact]
    public void Watch_DifferentTarget_SwitchesSubscription()
    {
        var (session, sink, registry, roster) = Build();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        roster.Entries.Add(new WatchRosterEntry(a.ToString(), "Alpha", ""));
        roster.Entries.Add(new WatchRosterEntry(b.ToString(), "Bravo", ""));

        sink.SimulateInput("watch " + a);
        sink.SimulateInput("watch " + b);

        registry.GetSinks(a).Should().BeEmpty();
        registry.GetSinks(b).Should().ContainSingle().Which.Should().BeSameAs(sink);
        session.CurrentTarget.Should().Be(b.ToString());
    }

    [Fact]
    public void PushRoster_SendsCurrentSnapshot()
    {
        var (session, sink, _, roster) = Build();
        roster.Entries.Add(new WatchRosterEntry(Guid.NewGuid().ToString(), "Alpha", ""));
        roster.Entries.Add(new WatchRosterEntry(Guid.NewGuid().ToString(), "Bravo", ""));

        session.PushRoster();

        sink.Rosters.Should().ContainSingle();
        ((IReadOnlyList<WatchRosterEntry>)sink.Rosters[0]).Should().HaveCount(2);
    }
}
