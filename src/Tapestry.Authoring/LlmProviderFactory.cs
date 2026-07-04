using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Recommend;

namespace Tapestry.Authoring;

/// <summary>Config-gated provider selection. Returns null when nothing should be bound
/// (LLM off and the stub not opted in) — the broker then reports IsEnabled == false and
/// consumers hide the recommend affordance.</summary>
public static class LlmProviderFactory
{
    public static IRecommendProvider? Create(
        RecommendLlmConfig config, RecommendPromptConfig promptConfig, Func<HttpClient> httpClientFactory,
        ILogger? logger = null, TapestryMetrics? metrics = null)
    {
        if (config.Enabled)
        {
            var client = new OpenAiCompatibleLlmClient(httpClientFactory());
            var roomBuilder = new RoomPromptBuilder(promptConfig);
            var areaBuilder = new AreaPromptBuilder(promptConfig);
            return new LlmRecommendProvider(client, roomBuilder, areaBuilder, config, logger, metrics);
        }
        if (config.UseStub)
        {
            // Snappy delay: the stub is a local-playtest mock + test fixture, not a latency sim.
            // A solo runs ~7 sequenced fill calls, so the default 2000ms would block ~10s.
            return new StaticStubRecommendProvider(delayMs: 400);
        }
        return null;
    }
}
