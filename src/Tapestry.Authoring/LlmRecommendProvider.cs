using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Recommend;

namespace Tapestry.Authoring;

/// <summary>Connection + behavior config for the LLM provider. <see cref="ApiKey"/> is
/// resolved from the environment by the composition layer; empty is fine for keyless local Ollama.</summary>
public sealed record RecommendLlmConfig(
    bool Enabled, bool UseStub, string BaseUrl, string Model, string ApiKey,
    bool RequiresKey, double Temperature, int MaxSentences, int Candidates, int TimeoutSeconds);

/// <summary>Author-intent-seeded recommendation. Name/description go to the LLM; exits stay
/// structural (rooms only). Never throws into the caller — all client failures degrade to Empty.</summary>
public sealed class LlmRecommendProvider : IRecommendProvider
{
    private readonly ILlmClient _client;
    private readonly RoomPromptBuilder _roomBuilder;
    private readonly AreaPromptBuilder _areaBuilder;
    private readonly RecommendLlmConfig _config;
    private readonly LlmOptions _opts;

    public LlmRecommendProvider(ILlmClient client, RoomPromptBuilder roomBuilder,
        AreaPromptBuilder areaBuilder, RecommendLlmConfig config)
    {
        _client = client;
        _roomBuilder = roomBuilder;
        _areaBuilder = areaBuilder;
        _config = config;
        _opts = new LlmOptions(config.Model, config.Temperature, config.TimeoutSeconds, config.BaseUrl, config.ApiKey);
    }

    public bool IsEnabled =>
        _config.Enabled
        && !string.IsNullOrWhiteSpace(_config.BaseUrl)
        && !string.IsNullOrWhiteSpace(_config.Model)
        && (!_config.RequiresKey || !string.IsNullOrEmpty(_config.ApiKey));

    public async Task<RecommendResult> RecommendAsync(RecommendRequest request)
    {
        var field = (request.Field ?? "").ToLowerInvariant();

        string system, user;
        if (request.Context is AreaData area)
        {
            (system, user) = _areaBuilder.Build(field, area, request.Hint);
        }
        else
        {
            var room = (RoomData)request.Context;
            // Exits stay structural — never burn an LLM call on direction math.
            if (field == "exits")
            {
                return ExitHeuristic.Suggest(room);
            }
            (system, user) = _roomBuilder.Build(field, room, request.Hint);
        }

        var n = field == "description" ? _config.Candidates : 1;
        var picks = new List<string>();
        for (var i = 0; i < n; i++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
                var text = await _client.CompleteAsync(system, user, _opts, cts.Token);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    picks.Add(NormalizeSuggestion(text));
                }
            }
            catch (Exception)
            {
                // Degrade gracefully — a failed candidate is dropped; never throw into the tick path.
            }
        }

        return picks.Count > 0 ? new RecommendResult(picks) : RecommendResult.Empty;
    }

    // Normalize a raw LLM suggestion for MUD use:
    //  (1) collapse all whitespace runs (incl. the newlines models love) to single spaces -- a
    //      description is one block the client word-wraps, and bare LF staircases over telnet;
    //  (2) strip ONE surrounding pair of quotes -- models wrap short names/titles in "..." which
    //      would otherwise be stored as part of the value (e.g. a room name of '"Burnt Heel Turn"').
    private static string NormalizeSuggestion(string text)
    {
        var s = System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ").Trim();
        if (s.Length >= 2)
        {
            var first = s[0];
            var last = s[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }
        }
        return s;
    }
}
