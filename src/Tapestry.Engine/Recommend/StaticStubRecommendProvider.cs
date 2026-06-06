using System.Collections.Generic;
using System.Threading.Tasks;
using Tapestry.Engine.Authoring;

namespace Tapestry.Engine.Recommend;

/// <summary>v1 trivial provider. Proves the contract + the async suspend/resume path.
/// Deliberate non-blocking delay (Task.Delay) — NEVER Thread.Sleep on the tick thread.</summary>
public sealed class StaticStubRecommendProvider : IRecommendProvider
{
    private readonly int _delayMs;
    private readonly bool _enabled;

    public StaticStubRecommendProvider(int delayMs = 2000, bool enabled = true)
    {
        _delayMs = delayMs;
        _enabled = enabled;
    }

    public bool IsEnabled => _enabled;

    public async Task<RecommendResult> RecommendAsync(RecommendRequest request)
    {
        if (_delayMs > 0)
        {
            await Task.Delay(_delayMs);
        }

        var field = request.Field?.ToLowerInvariant() ?? "";
        switch (field)
        {
            case "name":
                return new RecommendResult(new List<string> { "The Forgotten Hollow" });
            case "description":
                return new RecommendResult(new List<string>
                {
                    "Moss carpets the stones underfoot, and the air hangs cool and still.",
                    "A hush settles over the clearing; light filters thin through the canopy.",
                    "Roots knuckle up through the earth, and somewhere water trickles unseen.",
                });
            case "exits":
                return ExitHeuristic.Suggest((RoomData)request.Context);
            default:
                return RecommendResult.Empty;
        }
    }
}
