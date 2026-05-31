using System;
using System.Threading;
using System.Threading.Tasks;
using Tapestry.Authoring;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Recommend;
using Xunit;

namespace Tapestry.Engine.Tests;

public class LlmRecommendProviderTests
{
    // Fake client: returns a canned string, counts calls, or throws on demand.
    private sealed class FakeLlmClient : ILlmClient
    {
        private readonly string _response;
        private readonly bool _throw;
        public int Calls { get; private set; }

        public FakeLlmClient(string response = "A quiet stone chamber.", bool shouldThrow = false)
        {
            _response = response;
            _throw = shouldThrow;
        }

        public Task<string> CompleteAsync(string system, string user, LlmOptions opts, CancellationToken ct = default)
        {
            Calls++;
            if (_throw)
            {
                throw new InvalidOperationException("boom");
            }
            return Task.FromResult(_response);
        }
    }

    private static RecommendLlmConfig Config(
        bool enabled = true, string baseUrl = "http://localhost:11434/v1", string model = "qwen2.5:7b",
        string apiKey = "", bool requiresKey = false, int candidates = 3) =>
        new(Enabled: enabled, UseStub: false, BaseUrl: baseUrl, Model: model, ApiKey: apiKey,
            RequiresKey: requiresKey, Temperature: 0.8, MaxSentences: 2, Candidates: candidates, TimeoutSeconds: 30);

    private static LlmRecommendProvider Provider(FakeLlmClient client, RecommendLlmConfig config) =>
        new(client, new RoomPromptBuilder(RecommendPromptConfig.Default), config);

    [Fact]
    public async Task Description_returns_candidates_count_suggestions()
    {
        var client = new FakeLlmClient();
        var provider = Provider(client, Config(candidates: 3));
        var result = await provider.RecommendAsync(new RecommendRequest("description", new RoomData(), "a hall"));
        Assert.Equal(3, result.Suggestions.Count);
        Assert.Equal(3, client.Calls);
    }

    [Fact]
    public async Task Name_returns_one_suggestion()
    {
        var client = new FakeLlmClient();
        var provider = Provider(client, Config(candidates: 3));
        var result = await provider.RecommendAsync(new RecommendRequest("name", new RoomData(), "a hall"));
        Assert.Single(result.Suggestions);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Exits_served_by_heuristic_without_calling_the_client()
    {
        var client = new FakeLlmClient();
        var provider = Provider(client, Config());
        var data = new RoomData();
        data.Exits["north"] = "ns:x";

        var result = await provider.RecommendAsync(new RecommendRequest("exits", data));

        Assert.Equal(0, client.Calls);
        Assert.Contains("south", result.Suggestions);
        Assert.DoesNotContain("north", result.Suggestions);
    }

    [Fact]
    public async Task Throwing_client_degrades_to_empty()
    {
        var client = new FakeLlmClient(shouldThrow: true);
        var provider = Provider(client, Config(candidates: 3));
        var result = await provider.RecommendAsync(new RecommendRequest("description", new RoomData(), "a hall"));
        Assert.Empty(result.Suggestions);
    }

    [Theory]
    [InlineData(true, "http://x/v1", "m", "", false, true)]   // configured, no key needed -> enabled
    [InlineData(false, "http://x/v1", "m", "", false, false)] // disabled by config
    [InlineData(true, "", "m", "", false, false)]             // blank base_url
    [InlineData(true, "http://x/v1", "", "", false, false)]   // blank model
    [InlineData(true, "http://x/v1", "m", "", true, false)]   // requires key, none present
    [InlineData(true, "http://x/v1", "m", "sk-123", true, true)] // requires key, present
    public void IsEnabled_reflects_config(
        bool enabled, string baseUrl, string model, string apiKey, bool requiresKey, bool expected)
    {
        var provider = Provider(new FakeLlmClient(),
            Config(enabled: enabled, baseUrl: baseUrl, model: model, apiKey: apiKey, requiresKey: requiresKey));
        Assert.Equal(expected, provider.IsEnabled);
    }
}
