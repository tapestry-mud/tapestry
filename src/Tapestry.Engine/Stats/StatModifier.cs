namespace Tapestry.Engine.Stats;

public class StatModifier
{
    public string Source { get; }
    public StatType Stat { get; }
    public int Value { get; }
    public ModifierType ModifierType { get; }

    public StatModifier(string source, StatType stat, int value, ModifierType modifierType = ModifierType.Flat)
    {
        Source = source;
        Stat = stat;
        Value = value;
        ModifierType = modifierType;
    }
}
