using System.Collections.Generic;
using System.Threading.Tasks;
using Tapestry.Engine.Authoring;

namespace Tapestry.Engine.Recommend;

public sealed record RecommendRequest(string Field, RoomData Context);

public sealed record RecommendResult(IReadOnlyList<string> Suggestions)
{
    public static readonly RecommendResult Empty = new(new List<string>());
}

/// <summary>Pluggable field-suggestion provider. Always async-shaped, always a list.
/// The real OpenAPI/LLM provider swaps in here with no caller changes.</summary>
public interface IRecommendProvider
{
    Task<RecommendResult> RecommendAsync(RecommendRequest request);
}
