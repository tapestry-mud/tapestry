namespace Tapestry.Engine.Consumables;

public enum ConsumeReason
{
    Success,
    ItemNotFound,
    WrongConsumeMethod,
    NoCharges,
    Cancelled
}

public record ConsumableResult(
    bool Success,
    ConsumeReason Reason,
    string? ItemId = null,
    string? ItemName = null,
    string? ConsumeMethod = null,
    int SustenanceValue = 0,
    string? EffectId = null,
    int EffectDuration = 0,
    Dictionary<string, object>? EffectData = null
);
