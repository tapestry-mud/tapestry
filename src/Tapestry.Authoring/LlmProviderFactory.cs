using System;
using System.Net.Http;
using Tapestry.Engine.Recommend;

namespace Tapestry.Authoring;

/// <summary>Config-gated provider selection. Returns null when nothing should be bound
/// (LLM off and the stub not opted in) — the broker then reports IsEnabled == false and
/// consumers hide the recommend affordance.</summary>
public static class LlmProviderFactory
{
    public static IRecommendProvider? Create(
        RecommendLlmConfig config, RecommendPromptConfig promptConfig, Func<HttpClient> httpClientFactory)
    {
        if (config.Enabled)
        {
            var client = new OpenAiCompatibleLlmClient(httpClientFactory());
            var roomBuilder = new RoomPromptBuilder(promptConfig);
            var areaBuilder = new AreaPromptBuilder(promptConfig);
            return new LlmRecommendProvider(client, roomBuilder, areaBuilder, config);
        }
        if (config.UseStub)
        {
            return new StaticStubRecommendProvider();
        }
        return null;
    }
}
