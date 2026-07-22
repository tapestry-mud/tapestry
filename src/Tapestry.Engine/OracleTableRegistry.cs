using Tapestry.Shared;

namespace Tapestry.Engine;

public class OracleTableRegistry
{
    private readonly Dictionary<string, OracleTable> _tables =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(OracleTable table) { _tables[table.Id] = table; }
    public OracleTable? Get(string id) { return _tables.GetValueOrDefault(id); }
    public IEnumerable<OracleTable> All() { return _tables.Values; }
    public bool Contains(string id) { return _tables.ContainsKey(id); }

    /// <summary>Removes every table scoped to <paramref name="areaId"/>: ids matching
    /// "&lt;areaId&gt;:*". The colon is the id separator (OracleTable.OracleTableId), so the
    /// prefix cannot bleed into a sibling area whose slug string-prefixes this one. Returns
    /// the number removed; 0 is not an error.</summary>
    public int RemoveByArea(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) { return 0; }
        var prefix = areaId + ":";
        var doomed = _tables.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var key in doomed) { _tables.Remove(key); }
        return doomed.Count;
    }
}
