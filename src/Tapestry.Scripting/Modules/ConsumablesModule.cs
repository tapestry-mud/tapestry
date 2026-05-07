using Tapestry.Engine;
using Tapestry.Engine.Consumables;
using Tapestry.Engine.Sustenance;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ConsumablesModule : IJintApiModule
{
    private readonly ConsumableService _consumables;
    private readonly World _world;
    private readonly SustenanceConfig _sustenanceConfig;

    public ConsumablesModule(ConsumableService consumables, World world, SustenanceConfig sustenanceConfig)
    {
        _consumables = consumables;
        _world = world;
        _sustenanceConfig = sustenanceConfig;
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
                    itemType = result.ItemType,
                    sustenanceValue = result.SustenanceValue,
                    effectId = result.EffectId,
                    effectDuration = result.EffectDuration,
                    effectData = result.EffectData
                };
            }),

            getSustenance = new Func<string, int>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return 100; }
                var entity = _world.GetEntity(entityId);
                if (entity == null) { return 100; }
                return entity.TryGetProperty<int>("sustenance", out var sustenance) ? sustenance : 100;
            }),

            getSustenanceTier = new Func<string, string>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return "full"; }
                var entity = _world.GetEntity(entityId);
                if (entity == null) { return "full"; }
                var value = entity.TryGetProperty<int>("sustenance", out var sustenanceTier) ? sustenanceTier : 100;
                return _sustenanceConfig.GetTier(value);
            })
        };
    }
}
