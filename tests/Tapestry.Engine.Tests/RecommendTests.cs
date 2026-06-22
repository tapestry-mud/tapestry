using System.Collections.Generic;
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
        data.Exits["north"] = new ExitData { Target = "ns:x" };
        data.Exits["up"] = new ExitData { Target = "ns:y" };

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
        data.Exits["north"] = new ExitData { Target = "ns:x" };
        data.Exits["UP"] = new ExitData { Target = "ns:y" }; // case-insensitive

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

    [Fact]
    public void Stub_is_enabled_by_default_and_can_be_constructed_disabled()
    {
        Assert.True(new StaticStubRecommendProvider(delayMs: 0).IsEnabled);
        Assert.False(new StaticStubRecommendProvider(delayMs: 0, enabled: false).IsEnabled);
    }

    // A provider that records whether RecommendAsync was called — proves the broker
    // short-circuits without touching the provider when disabled.
    private sealed class CountingProvider : IRecommendProvider
    {
        public int Calls { get; private set; }
        public bool IsEnabled { get; }
        public CountingProvider(bool isEnabled) { IsEnabled = isEnabled; }
        public Task<RecommendResult> RecommendAsync(RecommendRequest request)
        {
            Calls++;
            return Task.FromResult(new RecommendResult(new List<string> { "x" }));
        }
    }

    [Fact]
    public void Broker_is_disabled_with_no_provider()
    {
        Assert.False(new RecommendBroker().IsEnabled);
    }

    [Fact]
    public void Broker_is_enabled_when_provider_is_enabled()
    {
        var broker = new RecommendBroker();
        broker.Register(new CountingProvider(isEnabled: true));
        Assert.True(broker.IsEnabled);
    }

    [Fact]
    public void Broker_SetEnabled_false_overrides_an_enabled_provider()
    {
        var broker = new RecommendBroker();
        broker.Register(new CountingProvider(isEnabled: true));
        broker.SetEnabled(false);
        Assert.False(broker.IsEnabled);
    }

    [Fact]
    public async Task Broker_short_circuits_to_empty_without_calling_disabled_provider()
    {
        var broker = new RecommendBroker();
        var provider = new CountingProvider(isEnabled: false);
        broker.Register(provider);

        var result = await broker.RecommendAsync(new RecommendRequest("name", new RoomData()));

        Assert.Empty(result.Suggestions);
        Assert.Equal(0, provider.Calls);
    }
}
