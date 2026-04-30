namespace Tapestry.Engine.Inventory;

public record RarityTierDefinition(
    string Key,
    int Order,
    string? DisplayText,
    (string Left, string Right)? Decorators,
    string Color,
    bool Visible
);
