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
}
