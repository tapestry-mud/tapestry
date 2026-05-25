using Jint.Native;

namespace Tapestry.Scripting.Interop;

/// <summary>
/// One registered export. <paramref name="Pack"/> is namespace form (e.g. "tapestry-survival").
/// Params/Returns are descriptive strings for introspection only (no runtime type enforcement).
/// </summary>
public sealed record ExportEntry(
    string Pack,
    string Name,
    JsValue Handler,
    string Description,
    IReadOnlyList<string> Params,
    string Returns,
    string Kind,
    IReadOnlyList<string> AppliesTo);

/// <summary>
/// Registry-mediated pack exports. Keyed by (pack, name) in namespace form.
/// Survives a single boot; cleared per load cycle if hot-reload is ever added.
/// </summary>
public sealed class PackExportRegistry
{
    private readonly Dictionary<(string Pack, string Name), ExportEntry> _entries = new();

    private static (string, string) Key(string pack, string name) =>
        (pack.ToLowerInvariant(), name);

    public void Register(ExportEntry entry)
    {
        if (!_entries.TryAdd(Key(entry.Pack, entry.Name), entry))
        {
            throw new InteropException(
                $"Pack '{entry.Pack}' has already registered an export named '{entry.Name}'.");
        }
    }

    public bool TryResolve(string pack, string name, out ExportEntry entry) =>
        _entries.TryGetValue(Key(pack, name), out entry!);

    public bool Has(string pack, string name) => _entries.ContainsKey(Key(pack, name));

    public IReadOnlyList<ExportEntry> GetAll() => _entries.Values.ToList();

    public void Clear() => _entries.Clear();
}
