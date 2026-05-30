using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapestry.Engine.Recommend;

/// <summary>v1 trivial provider. Proves the contract + the async suspend/resume path.
/// Deliberate non-blocking delay (Task.Delay) — NEVER Thread.Sleep on the tick thread.</summary>
public sealed class StaticStubRecommendProvider : IRecommendProvider
{
    private static readonly string[] AllDirections = { "north", "south", "east", "west", "up", "down" };
    private readonly int _delayMs;

    public StaticStubRecommendProvider(int delayMs = 2000)
    {
        _delayMs = delayMs;
    }

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
                var used = new HashSet<string>(
                    request.Context.Exits.Keys.Select(k => k.ToLowerInvariant()),
                    StringComparer.OrdinalIgnoreCase);
                return new RecommendResult(AllDirections.Where(d => !used.Contains(d)).ToList());
            default:
                return RecommendResult.Empty;
        }
    }
}
