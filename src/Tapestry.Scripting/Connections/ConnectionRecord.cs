namespace Tapestry.Scripting.Connections;

public class ConnectionSide
{
    public string Room { get; set; } = "";
    public string Type { get; set; } = "";  // "direction", "keyword", "one-way"
    public string? Direction { get; set; }
    public string? Keyword { get; set; }
    public string? DisplayName { get; set; }
}

public class ConnectionRecord
{
    public string Id { get; set; } = "";
    public ConnectionSide From { get; set; } = new();
    public ConnectionSide To { get; set; } = new();
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
