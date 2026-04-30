using Tapestry.Engine;
using Tapestry.Engine.Inventory;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class StackingModule : IJintApiModule
{
    private readonly StackingService _stackingService;
    private readonly World _world;

    public StackingModule(StackingService stackingService, World world)
    {
        _stackingService = stackingService;
        _world = world;
    }

    public string Namespace => "stacking";

    public object Build(JintEngine engine)
    {
        return new
        {
            addKey = new Action<string>(propertyName => _stackingService.AddKey(propertyName)),

            getStacks = new Func<string, object[]>(entityId =>
            {
                if (!Guid.TryParse(entityId, out var eid)) { return []; }
                var entity = _world.GetEntity(eid);
                if (entity == null) { return []; }
                var stacks = _stackingService.GetStacks(entity);
                return stacks.Select(s => (object)new
                {
                    templateId = s.TemplateId,
                    name = s.Name,
                    quantity = s.Quantity,
                    rarityKey = s.RarityKey,
                    essenceKey = s.EssenceKey,
                    itemIds = s.ItemIds.ToArray()
                }).ToArray();
            })
        };
    }
}
