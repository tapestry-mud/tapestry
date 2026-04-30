namespace Tapestry.Engine.Inventory;

public class EssenceRegistry
{
    private readonly Dictionary<string, EssenceDefinition> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public void Register(EssenceDefinition essence)
    {
        _byKey[essence.Key] = essence;
    }

    public EssenceDefinition? GetEssence(string key) => _byKey.GetValueOrDefault(key);

    public string Format(string? essenceKey)
    {
        if (essenceKey == null) { return string.Empty; }
        var essence = _byKey.GetValueOrDefault(essenceKey);
        if (essence == null) { return string.Empty; }
        return $"<essence.{essence.Key}>({essence.Glyph})</essence.{essence.Key}>";
    }
}
