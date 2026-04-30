namespace Tapestry.Engine.Ui;

public class Section
{
    public required IReadOnlyList<Row> Rows { get; init; }
    public RuleStyle SeparatorAbove { get; init; } = RuleStyle.Minor;
}

public enum RuleStyle { None, Minor, Major }
