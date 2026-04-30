namespace Tapestry.Engine.Ui;

public class Cell
{
    public required string Content { get; init; }
    public required CellWidth Width { get; init; }
    public Align Align { get; init; } = Align.Left;
    public bool Wrap { get; init; } = false;
}

public class ProgressCell : Cell
{
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public ProgressCell()
    {
        Content = string.Empty;
        Width = CellWidth.Fixed(0);
    }

    public long Value { get; init; }
    public long Max { get; init; }
}

public readonly struct CellWidth
{
    public bool IsFill { get; }
    public int Value { get; }

    private CellWidth(bool isFill, int value)
    {
        IsFill = isFill;
        Value = value;
    }

    public static CellWidth Fill { get; } = new(true, 0);
    public static CellWidth Fixed(int chars) => new(false, chars);
}
