namespace Tapestry.Engine.Ui;

public abstract class Row { }

public class EmptyRow : Row { }

public class TitleRow : Row
{
    public required string Left { get; init; }
    public string? Right { get; init; }
}

public class TextRow : Row
{
    public required string Content { get; init; }
    public Align Align { get; init; } = Align.Left;
    public bool Wrap { get; init; } = false;
}

public class CellRow : Row
{
    public required IReadOnlyList<Cell> Cells { get; init; }
    public bool ShowDividers { get; init; } = false;
}

public class FooterRow : Row
{
    public required string Content { get; init; }
}

public enum Align { Left, Right, Center }
