// src/Tapestry.Engine/Authoring/RoomSlugger.cs
using System.Text;

namespace Tapestry.Engine.Authoring;

/// <summary>Name → id-slug rules for authored-room re-key: lowercase, strip apostrophes,
/// collapse non-alphanumeric runs to single hyphens, trim hyphens, drop one leading
/// article (the/a/an). Pure and static — no World knowledge.</summary>
public static class RoomSlugger
{
    private static readonly string[] Articles = ["the-", "a-", "an-"];

    /// <summary>Slugify a room name; null when nothing usable survives (e.g. "!!!").</summary>
    public static string? Slugify(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        // Apostrophes vanish (raven's -> ravens) instead of becoming hyphens.
        var lowered = name.ToLowerInvariant().Replace("'", "").Replace("’", "");

        var sb = new StringBuilder(lowered.Length);
        var lastWasHyphen = false;
        foreach (var ch in lowered)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && sb.Length > 0)
            {
                sb.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = sb.ToString().Trim('-');

        foreach (var article in Articles)
        {
            if (slug.StartsWith(article, StringComparison.Ordinal))
            {
                slug = slug[article.Length..];
                break;
            }
        }

        return slug.Length == 0 ? null : slug;
    }

    /// <summary>Append -2, -3, … until <paramref name="taken"/> reports the slug free.
    /// Never fails — a rename is never blocked by a name collision.</summary>
    public static string Disambiguate(string slug, Func<string, bool> taken)
    {
        if (!taken(slug))
        {
            return slug;
        }

        var n = 2;
        while (taken($"{slug}-{n}"))
        {
            n++;
        }
        return $"{slug}-{n}";
    }
}
