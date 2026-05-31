using System.Linq;
using System.Threading.Tasks;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Recommend;
using Xunit;

namespace Tapestry.Engine.Tests;

public class RecommendTests
{
    [Fact]
    public async Task Broker_is_noop_when_no_provider()
    {
        var broker = new RecommendBroker();
        var result = await broker.RecommendAsync(new RecommendRequest("description", new RoomData()));
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task Stub_description_returns_three_suggestions()
    {
        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));
        var result = await broker.RecommendAsync(new RecommendRequest("description", new RoomData()));
        Assert.Equal(3, result.Suggestions.Count);
    }

    [Fact]
    public async Task Stub_exits_returns_available_minus_used()
    {
        var data = new RoomData();
        data.Exits["north"] = "ns:x";
        data.Exits["up"] = "ns:y";

        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));
        var result = await broker.RecommendAsync(new RecommendRequest("exits", data));

        Assert.DoesNotContain("north", result.Suggestions);
        Assert.DoesNotContain("up", result.Suggestions);
        Assert.Contains("south", result.Suggestions);
        Assert.Contains("east", result.Suggestions);
        Assert.Contains("west", result.Suggestions);
        Assert.Contains("down", result.Suggestions);
    }

    [Fact]
    public void ExitHeuristic_offers_unused_directions_only()
    {
        var data = new RoomData();
        data.Exits["north"] = "ns:x";
        data.Exits["UP"] = "ns:y"; // case-insensitive

        var result = Tapestry.Engine.Recommend.ExitHeuristic.Suggest(data);

        Assert.DoesNotContain("north", result.Suggestions);
        Assert.DoesNotContain("up", result.Suggestions);
        Assert.Contains("south", result.Suggestions);
        Assert.Contains("east", result.Suggestions);
        Assert.Contains("west", result.Suggestions);
        Assert.Contains("down", result.Suggestions);
    }

    [Fact]
    public void RecommendRequest_hint_defaults_to_null_and_round_trips()
    {
        var noHint = new RecommendRequest("description", new RoomData());
        Assert.Null(noHint.Hint);

        var withHint = new RecommendRequest("description", new RoomData(), "a hallway to the gate");
        Assert.Equal("a hallway to the gate", withHint.Hint);
    }
}
