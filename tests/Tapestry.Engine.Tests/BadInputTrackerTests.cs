using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class BadInputTrackerTests
{
    private static BadInputTracker Make() =>
        new BadInputTracker(NullLogger<BadInputTracker>.Instance, null);

    [Fact]
    public void Record_NewVerb_CreatesEntry()
    {
        var tracker = Make();
        tracker.Record("frobble", "frobble the widget", "Alice", "room1");

        var entries = tracker.GetEntries();
        entries.Should().ContainSingle(e => e.Verb == "frobble" && e.Count == 1);
    }

    [Fact]
    public void Record_ExistingVerb_IncrementsCount()
    {
        var tracker = Make();
        tracker.Record("frobble", "frobble 1", "Alice", "room1");
        tracker.Record("frobble", "frobble 2", "Alice", "room1");

        tracker.GetEntries().Single(e => e.Verb == "frobble").Count.Should().Be(2);
    }

    [Fact]
    public void Record_LowercasesAndTrimsVerb()
    {
        var tracker = Make();
        tracker.Record("  FROBBLE  ", "FROBBLE", "Alice", "room1");

        tracker.GetEntries().Should().ContainSingle(e => e.Verb == "frobble");
    }

    [Fact]
    public void Clear_ResetsAllEntries()
    {
        var tracker = Make();
        tracker.Record("frobble", "frobble", "Alice", "room1");
        tracker.Clear();

        tracker.GetEntries().Should().BeEmpty();
    }

    [Fact]
    public void GetEntries_ReturnsSortedByCountDescending()
    {
        var tracker = Make();
        tracker.Record("a", "a", "Alice", "room1");
        tracker.Record("b", "b", "Alice", "room1");
        tracker.Record("b", "b", "Alice", "room1");
        tracker.Record("b", "b", "Alice", "room1");
        tracker.Record("c", "c", "Alice", "room1");
        tracker.Record("c", "c", "Alice", "room1");

        var entries = tracker.GetEntries();
        entries[0].Verb.Should().Be("b");
        entries[1].Verb.Should().Be("c");
        entries[2].Verb.Should().Be("a");
    }

    [Fact]
    public void Record_SetsFirstSeenOnFirstRecord()
    {
        var tracker = Make();
        var before = DateTime.UtcNow;
        tracker.Record("frobble", "frobble", "Alice", "room1");
        var after = DateTime.UtcNow;

        var entry = tracker.GetEntries().Single();
        entry.FirstSeen.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Record_UpdatesLastSeenOnSubsequentRecords()
    {
        var tracker = Make();
        tracker.Record("frobble", "frobble", "Alice", "room1");
        var firstSeen = tracker.GetEntries().Single().FirstSeen;

        tracker.Record("frobble", "frobble", "Alice", "room1");

        var entry = tracker.GetEntries().Single();
        entry.FirstSeen.Should().Be(firstSeen);
        entry.LastSeen.Should().BeOnOrAfter(firstSeen);
    }
}
