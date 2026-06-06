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
/// structural. Never throws into the caller — all client failures degrade to Empty.</summary>
public sealed class LlmRecommendProvider : IRecommendProvider
{
    private readonly ILlmClient _client;
    private readonly RoomPromptBuilder _builder;
    private readonly RecommendLlmConfig _config;
    private readonly LlmOptions _opts;

    public LlmRecommendProvider(ILlmClient client, RoomPromptBuilder builder, RecommendLlmConfig config)
    {
        _client = client;
        _builder = builder;
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

        // Exits stay structural — never burn an LLM call on direction math.
        if (field == "exits")
        {
            return ExitHeuristic.Suggest((RoomData)request.Context);
        }

        var (system, user) = _builder.Build(field, (RoomData)request.Context, request.Hint);
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
                    picks.Add(text);
                }
            }
            catch (Exception)
            {
                // Degrade gracefully — a failed candidate is dropped; never throw into the tick path.
            }
        }

        return picks.Count > 0 ? new RecommendResult(picks) : RecommendResult.Empty;
    }
}
