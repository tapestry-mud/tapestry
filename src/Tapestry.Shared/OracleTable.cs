namespace Tapestry.Shared;

public class OracleTable
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public List<OracleEntry> Entries { get; set; } = new();
    public string SourcePack { get; set; } = "";

    public static string OracleTableId(string areaId, string kind) => $"{areaId}:{kind}";
}

public class OracleEntry
{
    public int W { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Desc { get; set; } = "";
    public string BalanceRef { get; set; } = "";
    public string Rarity { get; set; } = "";
    public Dictionary<string, string> Extra { get; set; } = new();
}
