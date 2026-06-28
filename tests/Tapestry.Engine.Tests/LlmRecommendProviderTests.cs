using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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

        public Task<string> CompleteAsync(string system, string user, LlmOptions opts, string? responseSchema = null, CancellationToken ct = default)
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

    // Records the last user prompt so tests can assert which builder path was taken.
    private sealed class CapturingLlmClient : ILlmClient
    {
        private readonly string _response;
        public string? LastUser { get; private set; }

        public CapturingLlmClient(string response = "ok") { _response = response; }

        public Task<string> CompleteAsync(string system, string user, LlmOptions opts, string? responseSchema = null, CancellationToken ct = default)
        {
            LastUser = user;
            return Task.FromResult(_response);
        }
    }

    private static LlmRecommendProvider Provider(FakeLlmClient client, RecommendLlmConfig config) =>
        new(client, new RoomPromptBuilder(RecommendPromptConfig.Default), new AreaPromptBuilder(RecommendPromptConfig.Default), config);

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
    public async Task Collapses_multiline_llm_output_to_a_single_line()
    {
        // Models emit multi-paragraph prose with embedded newlines; over telnet bare LF staircases.
        var client = new CapturingLlmClient("First paragraph.\n\n  Second paragraph.\nThird.");
        var provider = new LlmRecommendProvider(
            client,
            new RoomPromptBuilder(RecommendPromptConfig.Default),
            new AreaPromptBuilder(RecommendPromptConfig.Default),
            Config());

        var result = await provider.RecommendAsync(
            new RecommendRequest("short", new AreaData { Id = "a", Name = "A" }, null));

        Assert.Single(result.Suggestions);
        Assert.Equal("First paragraph. Second paragraph. Third.", result.Suggestions[0]);
        Assert.DoesNotContain("\n", result.Suggestions[0]);
    }

    [Fact]
    public async Task Strips_a_surrounding_quote_pair_from_a_suggestion()
    {
        // Models wrap short names/titles in quotes; the value must not keep them.
        var client = new CapturingLlmClient("\"Burnt Heel Turn\"");
        var provider = new LlmRecommendProvider(
            client,
            new RoomPromptBuilder(RecommendPromptConfig.Default),
            new AreaPromptBuilder(RecommendPromptConfig.Default),
            Config());

        var result = await provider.RecommendAsync(
            new RecommendRequest("name", new RoomData(), null));

        Assert.Single(result.Suggestions);
        Assert.Equal("Burnt Heel Turn", result.Suggestions[0]);
    }

    [Fact]
    public async Task Exits_served_by_heuristic_without_calling_the_client()
    {
        var client = new FakeLlmClient();
        var provider = Provider(client, Config());
        var data = new RoomData();
        data.Exits["north"] = new ExitData { Target = "ns:x" };

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

    [Fact]
    public async System.Threading.Tasks.Task AreaContext_UsesAreaPromptBuilder()
    {
        var captured = new CapturingLlmClient("ok");
        var provider = new LlmRecommendProvider(
            captured,
            new RoomPromptBuilder(RecommendPromptConfig.Default),
            new AreaPromptBuilder(RecommendPromptConfig.Default),
            Config());

        var area = new AreaData { Id = "road-to-tar-valon", Name = "Road to Tar Valon", Theme = "Grim." };
        await provider.RecommendAsync(new RecommendRequest("short", area, null));

        // AreaPromptBuilder always emits "Area: <Name>." — RoomPromptBuilder never would.
        Assert.Contains("Road to Tar Valon", captured.LastUser);
    }
}

public class LlmProviderFactoryTests
{
    private static RecommendLlmConfig Cfg(bool enabled, bool useStub) =>
        new(Enabled: enabled, UseStub: useStub, BaseUrl: "http://x/v1", Model: "m", ApiKey: "",
            RequiresKey: false, Temperature: 0.8, MaxSentences: 2, Candidates: 3, TimeoutSeconds: 30);

    [Fact]
    public void Enabled_binds_the_llm_provider()
    {
        var p = LlmProviderFactory.Create(Cfg(enabled: true, useStub: false),
            RecommendPromptConfig.Default, () => new System.Net.Http.HttpClient());
        Assert.IsType<LlmRecommendProvider>(p);
    }

    [Fact]
    public void Disabled_without_stub_binds_nothing()
    {
        var p = LlmProviderFactory.Create(Cfg(enabled: false, useStub: false),
            RecommendPromptConfig.Default, () => new System.Net.Http.HttpClient());
        Assert.Null(p);
    }

    [Fact]
    public void Disabled_with_use_stub_binds_the_stub()
    {
        var p = LlmProviderFactory.Create(Cfg(enabled: false, useStub: true),
            RecommendPromptConfig.Default, () => new System.Net.Http.HttpClient());
        Assert.IsType<Tapestry.Engine.Recommend.StaticStubRecommendProvider>(p);
    }
}

public class OpenAiCompatibleLlmClientTests
{
    // Captures the outgoing request and returns a canned chat-completions response.
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _responseContent;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public CapturingHandler(string assistantText)
        {
            _responseContent = JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { role = "assistant", content = assistantText } } }
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public async Task Posts_to_chat_completions_with_bearer_and_parses_sanitized_content()
    {
        var handler = new CapturingHandler("<|im_start|>A still, vaulted hall.<|im_end|>");
        var client = new OpenAiCompatibleLlmClient(new HttpClient(handler));
        var opts = new LlmOptions("qwen2.5:7b", 0.8, 30, "http://localhost:11434/v1", "sk-test");

        var text = await client.CompleteAsync("sys", "usr", opts);

        Assert.Equal("A still, vaulted hall.", text); // sanitized + trimmed
        Assert.Equal("http://localhost:11434/v1/chat/completions", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test", handler.LastRequest.Headers.Authorization.Parameter);
        Assert.Contains("\"model\":\"qwen2.5:7b\"", handler.LastBody);
        Assert.Contains("\"sys\"", handler.LastBody);
        Assert.Contains("\"usr\"", handler.LastBody);
    }

    [Fact]
    public async Task Structured_request_carries_response_format_and_returns_raw_json()
    {
        var json = "{\"mobs\":[{\"name\":\"A\",\"desc\":\"b\"}]}";
        var handler = new CapturingHandler(json);
        var client = new OpenAiCompatibleLlmClient(new HttpClient(handler));
        var opts = new LlmOptions("m", 0.5, 30, "http://x/v1", "");
        var schema = "{\"type\":\"object\",\"properties\":{\"mobs\":{\"type\":\"array\"}},\"required\":[\"mobs\"],\"additionalProperties\":false}";

        var result = await client.CompleteAsync("sys", "usr", opts, schema);

        Assert.Contains("\"response_format\"", handler.LastBody);
        Assert.Contains("\"json_schema\"", handler.LastBody);
        Assert.Contains("\"strict\":true", handler.LastBody);
        Assert.Contains("\"mobs\"", handler.LastBody); // schema embedded as an object, not a quoted string
        Assert.Equal(json, result); // raw JSON returned, not free-text normalized
    }
}
