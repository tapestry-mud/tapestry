using System.Collections.Generic;
using System.Threading.Tasks;
using Tapestry.Engine.Authoring;

namespace Tapestry.Engine.Recommend;

public sealed record RecommendRequest(string Field, RoomData Context, string? Hint = null);

public sealed record RecommendResult(IReadOnlyList<string> Suggestions)
{
    public static readonly RecommendResult Empty = new(new List<string>());
}

/// <summary>Pluggable field-suggestion provider. Always async-shaped, always a list.
/// The real OpenAPI/LLM provider swaps in here with no caller changes.</summary>
public interface IRecommendProvider
{
    /// <summary>Can this provider produce real suggestions right now? False when
    /// misconfigured/unusable (no key where one is required, missing base_url/model, ...).
    /// A cheap synchronous check — no I/O; never pings the endpoint.</summary>
    bool IsEnabled { get; }

    Task<RecommendResult> RecommendAsync(RecommendRequest request);
}
