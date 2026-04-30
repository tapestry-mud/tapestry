namespace Tapestry.Engine.Ui;

public class Panel
{
    public int Width { get; init; } = 80;
    public required IReadOnlyList<Section> Sections { get; init; }
}
