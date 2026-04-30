namespace Tapestry.Engine.Inventory;

public static class KeywordMatcher
{
    public static Entity? FindByKeyword(IEnumerable<Entity> entities, string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return null;
        }

        // Ordinal: "2.ring" means second entity matching "ring"
        var dotIndex = keyword.IndexOf('.');
        if (dotIndex > 0 && int.TryParse(keyword[..dotIndex], out var ordinal))
        {
            var actualKeyword = keyword[(dotIndex + 1)..];
            var matches = entities.Where(e => MatchesKeyword(e, actualKeyword)).ToList();
            if (ordinal >= 1 && ordinal <= matches.Count)
            {
                return matches[ordinal - 1];
            }
            return null;
        }

        // Exact tag match first, then prefix match
        return entities.FirstOrDefault(e => e.HasTag(keyword))
            ?? entities.FirstOrDefault(e => HasTagPrefix(e, keyword));
    }

    public static List<Entity> FindAllByKeyword(IEnumerable<Entity> entities, string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return new List<Entity>();
        }

        // "all" — return everything
        if (keyword.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return entities.ToList();
        }

        // "all.sword" — return all matching "sword"
        if (keyword.StartsWith("all.", StringComparison.OrdinalIgnoreCase))
        {
            var actualKeyword = keyword[4..];
            return entities.Where(e => MatchesKeyword(e, actualKeyword)).ToList();
        }

        // Single keyword — return all matching (exact or prefix)
        return entities.Where(e => MatchesKeyword(e, keyword)).ToList();
    }

    private static bool MatchesKeyword(Entity entity, string keyword)
    {
        return entity.HasTag(keyword) || HasTagPrefix(entity, keyword);
    }

    private static bool HasTagPrefix(Entity entity, string prefix)
    {
        return entity.Tags.Any(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && t.Length > prefix.Length);
    }
}
