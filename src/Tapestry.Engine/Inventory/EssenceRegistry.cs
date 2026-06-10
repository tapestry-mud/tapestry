using Tapestry.Engine.Registration;

namespace Tapestry.Engine.Inventory;

public class EssenceRegistry
{
    private readonly Dictionary<string, EssenceDefinition> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly RegistrationGate? _gate;

    public EssenceRegistry(RegistrationGate? gate = null)
    {
        _gate = gate;
    }

    public void Register(EssenceDefinition essence)
    {
        _gate?.AssertCommitScope("essence", essence.Key);
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
