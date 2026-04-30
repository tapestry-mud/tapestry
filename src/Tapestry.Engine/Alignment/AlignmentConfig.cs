// src/Tapestry.Engine/Alignment/AlignmentConfig.cs
namespace Tapestry.Engine.Alignment;

public class AlignmentConfig
{
    public int EvilThreshold { get; private set; } = -350;
    public int GoodThreshold { get; private set; } = 350;

    public void Configure(int evil, int good)
    {
        EvilThreshold = evil;
        GoodThreshold = good;
    }

    public string BucketFor(int alignment)
    {
        if (alignment <= EvilThreshold) { return "evil"; }
        if (alignment >= GoodThreshold) { return "good"; }
        return "neutral";
    }

    // Resolves bucket names to min/max at registration time (resolve-once).
    // Thresholds used are the values at the moment of this call.
    public (int? min, int? max) ResolveBuckets(IEnumerable<string> buckets)
    {
        var set = new HashSet<string>(buckets);
        bool evil = set.Contains("evil");
        bool neutral = set.Contains("neutral");
        bool good = set.Contains("good");

        if (evil && !neutral && !good) { return (null, EvilThreshold); }
        if (good && !neutral && !evil) { return (GoodThreshold, null); }
        if (neutral && !evil && !good) { return (EvilThreshold + 1, GoodThreshold - 1); }
        if (evil && neutral && !good)  { return (null, GoodThreshold - 1); }
        if (good && neutral && !evil)  { return (EvilThreshold + 1, null); }
        return (null, null); // degenerate: evil+good treated as unrestricted
    }
}
