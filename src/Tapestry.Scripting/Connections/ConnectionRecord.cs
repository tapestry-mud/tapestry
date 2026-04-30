namespace Tapestry.Scripting.Connections;

public record ConnectionSide(
    string Room,
    string Type,          // "direction", "keyword", "one-way"
    string? Direction,
    string? Keyword,
    string? DisplayName
);

public record ConnectionRecord(
    string Id,
    ConnectionSide From,
    ConnectionSide To,
    string CreatedBy,
    DateTimeOffset CreatedAt
);
