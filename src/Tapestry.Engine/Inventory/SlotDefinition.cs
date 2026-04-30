namespace Tapestry.Engine.Inventory;

public class SlotDefinition
{
    public string Name { get; }
    public string Display { get; }
    public int Max { get; }

    public SlotDefinition(string name, string display, int max)
    {
        Name = name;
        Display = display;
        Max = max;
    }
}
