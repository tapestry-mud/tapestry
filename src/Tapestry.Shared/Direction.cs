namespace Tapestry.Shared;

public enum Direction
{
    North, South, East, West, Up, Down
}

public static class DirectionExtensions
{
    private static readonly Dictionary<string, Direction> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["n"] = Direction.North, ["north"] = Direction.North,
        ["s"] = Direction.South, ["south"] = Direction.South,
        ["e"] = Direction.East, ["east"] = Direction.East,
        ["w"] = Direction.West, ["west"] = Direction.West,
        ["u"] = Direction.Up, ["up"] = Direction.Up,
        ["d"] = Direction.Down, ["down"] = Direction.Down,
    };

    public static bool TryParse(string input, out Direction direction)
    {
        return Aliases.TryGetValue(input, out direction);
    }

    public static Direction Opposite(this Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            _ => throw new ArgumentOutOfRangeException(nameof(dir))
        };
    }

    public static string ToShortString(this Direction dir)
    {
        return dir switch
        {
            Direction.North => "N",
            Direction.South => "S",
            Direction.East => "E",
            Direction.West => "W",
            Direction.Up => "U",
            Direction.Down => "D",
            _ => dir.ToString()
        };
    }
}
