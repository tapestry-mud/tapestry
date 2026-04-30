using Tapestry.Engine;
using Tapestry.Engine.Items;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ItemsModule : IJintApiModule
{
    private readonly ItemRegistry _itemRegistry;
    private readonly World _world;

    public ItemsModule(ItemRegistry itemRegistry, World world)
    {
        _itemRegistry = itemRegistry;
        _world = world;
    }

    public string Namespace => "items";

    public object Build(JintEngine engine)
    {
        return new
        {
            createFromTemplate = new Func<string, object?>(templateId =>
            {
                var item = _itemRegistry.CreateItem(templateId);
                if (item == null)
                {
                    return null;
                }

                _world.TrackEntity(item);

                return new
                {
                    id = item.Id.ToString(),
                    name = item.Name,
                    type = item.Type,
                    templateId = templateId
                };
            }),

            spawnToInventory = new Func<string, string, object?>(( templateId, entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId))
                {
                    return null;
                }

                var entity = _world.GetEntity(entityId);
                if (entity == null)
                {
                    return null;
                }

                var item = _itemRegistry.CreateItem(templateId);
                if (item == null)
                {
                    return null;
                }

                _world.TrackEntity(item);
                entity.AddToContents(item);

                return new
                {
                    id = item.Id.ToString(),
                    name = item.Name,
                    type = item.Type,
                    templateId = templateId
                };
            }),

            hasTemplate = new Func<string, bool>(templateId =>
            {
                return _itemRegistry.HasTemplate(templateId);
            })
        };
    }
}
