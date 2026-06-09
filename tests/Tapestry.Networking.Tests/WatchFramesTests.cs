using System.Text.Json;
using FluentAssertions;
using Tapestry.Networking;

namespace Tapestry.Networking.Tests;

public class WatchFramesTests
{
    [Fact]
    public void Watch_SerializesTypeAndData()
    {
        using var doc = JsonDocument.Parse(WatchFrames.Watch("A goblin dies.\r\n"));
        doc.RootElement.GetProperty("type").GetString().Should().Be("watch");
        doc.RootElement.GetProperty("data").GetString().Should().Be("A goblin dies.\r\n");
    }

    [Fact]
    public void Status_SerializesTypeAndData()
    {
        using var doc = JsonDocument.Parse(WatchFrames.Status("Now watching Mallek."));
        doc.RootElement.GetProperty("type").GetString().Should().Be("status");
        doc.RootElement.GetProperty("data").GetString().Should().Be("Now watching Mallek.");
    }

    [Fact]
    public void Roster_SerializesEntriesWithCamelCaseFields()
    {
        var roster = new[] { new { EntityId = "id-1", Name = "Mallek", RoomId = "lf:square" } };

        using var doc = JsonDocument.Parse(WatchFrames.Roster(roster));

        doc.RootElement.GetProperty("type").GetString().Should().Be("roster");
        var first = doc.RootElement.GetProperty("data")[0];
        first.GetProperty("entityId").GetString().Should().Be("id-1");
        first.GetProperty("name").GetString().Should().Be("Mallek");
        first.GetProperty("roomId").GetString().Should().Be("lf:square");
    }
}
