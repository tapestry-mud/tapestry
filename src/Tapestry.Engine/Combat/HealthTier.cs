// src/Tapestry.Engine/Combat/HealthTier.cs
namespace Tapestry.Engine.Combat;

public static class HealthTier
{
    public static (string Tier, string Text) Get(int hp, int maxHp)
    {
        if (maxHp <= 0)
        {
            return ("near death", "is near death");
        }

        var pct = (int)(Math.Clamp(hp, 0, maxHp) * 100.0 / maxHp);

        return pct switch
        {
            100     => ("perfect", "is in perfect health"),
            >= 75   => ("few scratches", "has a few scratches"),
            >= 50   => ("small wounds", "has some small wounds"),
            >= 35   => ("wounded", "is wounded"),
            >= 20   => ("badly wounded", "is badly wounded"),
            >= 10   => ("bleeding profusely", "is bleeding profusely"),
            _       => ("near death", "is near death"),
        };
    }
}
