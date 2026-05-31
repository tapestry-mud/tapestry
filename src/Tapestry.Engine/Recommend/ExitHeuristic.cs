using System;
using System.Collections.Generic;
using System.Linq;
using Tapestry.Engine.Authoring;

namespace Tapestry.Engine.Recommend;

/// <summary>Deterministic exit suggestion: offer the standard directions not already used.
/// Shared by the stub and the LLM provider — exits are never sent to a model.</summary>
public static class ExitHeuristic
{
    private static readonly string[] AllDirections = { "north", "south", "east", "west", "up", "down" };

    public static RecommendResult Suggest(RoomData context)
    {
        var used = new HashSet<string>(
            context.Exits.Keys.Select(k => k.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
        return new RecommendResult(AllDirections.Where(d => !used.Contains(d)).ToList());
    }
}
