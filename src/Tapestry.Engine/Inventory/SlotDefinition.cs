namespace Tapestry.Engine.Inventory;

public class SlotDefinition
{
    public string Name { get; }
    public string Display { get; }
    public int Max { get; }
    public string Scope { get; }

    public SlotDefinition(string name, string display, int max, string scope = "engine")
    {
        Name = name;
        Display = display;
        Max = max;
        Scope = scope;
    }

    public bool IsEngineSlot => Scope == "engine";
}
