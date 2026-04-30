namespace Tapestry.Engine.Color;

public class AnsiPair
{
    public string Open { get; }
    public string Close { get; }

    public AnsiPair(string open, string close)
    {
        Open = open;
        Close = close;
    }
}

public class ThemeEntry
{
    public string? Fg { get; set; }
    public string? Bg { get; set; }
    public string? Html { get; set; }
}
