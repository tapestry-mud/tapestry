namespace Tapestry.Engine.Flow;

/// <summary>Flow-scoped working memory. Lives exactly as long as the FlowInstance that owns
/// it and is NEVER serialized - there is no code path from here to PlayerSerializer. Step
/// scripts read and write it via the entity proxy's <c>scratch</c> handle instead of the
/// entity property bag, so wizard answers stop leaking into player.yaml and keep real types.</summary>
public interface IFlowScratch
{
    /// <summary>The stored value (real type preserved), or null if absent.</summary>
    object? Get(string key);

    /// <summary>Store <paramref name="value"/> under <paramref name="key"/>.</summary>
    void Set(string key, object? value);

    /// <summary>True when <paramref name="key"/> has been set.</summary>
    bool Has(string key);
}

public sealed class FlowScratch : IFlowScratch
{
    private readonly Dictionary<string, object?> _store;

    public FlowScratch(IReadOnlyDictionary<string, object?>? seed = null)
    {
        _store = seed != null
            ? new Dictionary<string, object?>(seed)
            : new Dictionary<string, object?>();
    }

    public object? Get(string key) => _store.TryGetValue(key, out var v) ? v : null;

    public void Set(string key, object? value) => _store[key] = value;

    public bool Has(string key) => _store.ContainsKey(key);
}
