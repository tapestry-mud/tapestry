// src/Tapestry.Engine/Combat/DiceRoller.cs
using System.Text.RegularExpressions;

namespace Tapestry.Engine.Combat;

public static partial class DiceRoller
{
    private static readonly Random DefaultRandom = new();

    [GeneratedRegex(@"^(\d+)d(\d+)([+-]\d+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex DicePattern();

    public static int Roll(string notation, Random? random = null)
    {
        var rng = random ?? DefaultRandom;
        var match = DicePattern().Match(notation.Trim());

        if (!match.Success)
        {
            return 0;
        }

        var count = int.Parse(match.Groups[1].Value);
        var sides = int.Parse(match.Groups[2].Value);
        var modifier = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        var total = 0;
        for (var i = 0; i < count; i++)
        {
            total += rng.Next(1, sides + 1);
        }

        return total + modifier;
    }

    public static int RollD20(Random? random = null)
    {
        var rng = random ?? DefaultRandom;
        return rng.Next(1, 21);
    }
}
