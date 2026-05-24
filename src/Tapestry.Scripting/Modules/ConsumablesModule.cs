using Tapestry.Engine;
using Tapestry.Engine.Consumables;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ConsumablesModule : IJintApiModule
{
    private readonly ConsumableService _consumables;

    public ConsumablesModule(ConsumableService consumables)
    {
        _consumables = consumables;
    }

    public string Namespace => "consumables";

    public object Build(JintEngine engine)
    {
        return new
        {
            consume = new Func<string, string, object?>((entityIdStr, itemIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return null; }
                if (!Guid.TryParse(itemIdStr, out var itemId)) { return null; }

                var result = _consumables.Consume(entityId, itemId);
                return new
                {
                    success = result.Success,
                    reason = result.Reason.ToString().ToLower(),
                    itemId = result.ItemId,
                    itemName = result.ItemName,
                    consumeMethod = result.ConsumeMethod,
                    sustenanceValue = result.SustenanceValue,
                    effectId = result.EffectId,
                    effectDuration = result.EffectDuration,
                    effectData = result.EffectData
                };
            })
        };
    }
}
