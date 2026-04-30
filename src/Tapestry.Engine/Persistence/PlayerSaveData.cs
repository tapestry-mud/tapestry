using Tapestry.Engine.Stats;

namespace Tapestry.Engine.Persistence;

public class PlayerSaveData
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Type { get; set; } = "player";
    public string Location { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public StatsSaveData Stats { get; set; } = new();
    public Dictionary<string, object?> Properties { get; set; } = new();
    public Dictionary<string, string> Equipment { get; set; } = new();
    public List<string> Inventory { get; set; } = new();
    public List<ItemSaveData> Items { get; set; } = new();
    public List<CorpseSaveData> Corpses { get; set; } = new();
}

public class StatsSaveData
{
    public BaseStatsSaveData Base { get; set; } = new();
    public VitalsSaveData Vitals { get; set; } = new();
    public List<ModifierSaveData> Modifiers { get; set; } = new();
}

public class BaseStatsSaveData
{
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Luck { get; set; }
    public int MaxHp { get; set; }
    public int MaxResource { get; set; }
    public int MaxMovement { get; set; }
}

public class VitalsSaveData
{
    public int Hp { get; set; }
    public int Resource { get; set; }
    public int Movement { get; set; }
}

public class ModifierSaveData
{
    public string Source { get; set; } = "";
    public string Stat { get; set; } = "";
    public int Value { get; set; }
}

public class ItemSaveData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Container { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object?> Properties { get; set; } = new();
}

public class CorpseSaveData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object?> Properties { get; set; } = new();
    public List<string> Contents { get; set; } = new();
}
