// src/Tapestry.Engine/Stats/StatDisplayNames.cs
namespace Tapestry.Engine.Stats;

public class StatDisplayNames
{
    private readonly Dictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["strength"] = "Strength",
        ["intelligence"] = "Intelligence",
        ["wisdom"] = "Wisdom",
        ["dexterity"] = "Dexterity",
        ["constitution"] = "Constitution",
        ["luck"] = "Luck",
        ["hp"] = "HP",
        ["resource"] = "Mana",
        ["movement"] = "Movement"
    };

    public void SetDisplayName(string statName, string displayName)
    {
        _overrides[statName] = displayName;
    }

    public string GetDisplayName(string statName)
    {
        if (_overrides.TryGetValue(statName, out var name))
        {
            return name;
        }

        if (Defaults.TryGetValue(statName, out var defaultName))
        {
            return defaultName;
        }

        return statName;
    }
}
