namespace Tapestry.Engine.Consumables;

public enum ConsumeReason
{
    Success,
    ItemNotFound,
    WrongItemType,
    NoCharges,
    Cancelled
}

public record ConsumableResult(
    bool Success,
    ConsumeReason Reason,
    string? ItemId = null,
    string? ItemName = null,
    string? ItemType = null,
    int SustenanceValue = 0,
    string? EffectId = null,
    int EffectDuration = 0,
    Dictionary<string, object>? EffectData = null
);
