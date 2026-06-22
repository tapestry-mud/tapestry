using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Mobs;

namespace Tapestry.Scripting.Services;

public class ApiMobs
{
    private readonly World _world;
    private readonly MobAIManager _mobAIManager;
    private readonly SpawnManager _spawnManager;

    public ApiMobs(World world, MobAIManager mobAIManager, SpawnManager spawnManager)
    {
        _world = world;
        _mobAIManager = mobAIManager;
        _spawnManager = spawnManager;
    }

    public Dictionary<string, object?>? GetEntityProperties(string entityIdStr)
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

        return new Dictionary<string, object?>(entity.GetAllProperties());
    }

    public long GetMobTicksSinceLastAction(string entityIdStr)
    {
        if (!Guid.TryParse(entityIdStr, out var entityId))
        {
            return 0;
        }

        return _mobAIManager.GetTicksSinceLastAction(entityId);
    }

    public void RecordMobAction(string entityIdStr)
    {
        if (Guid.TryParse(entityIdStr, out var entityId))
        {
            _mobAIManager.RecordAction(entityId);
        }
    }

    public object? SpawnMob(JsValue options)
    {
        if (options is not ObjectInstance obj) { return null; }

        var templateVal = obj.Get("template");
        var roomVal = obj.Get("roomId");
        if (templateVal.Type != Types.String || roomVal.Type != Types.String) { return null; }

        var templateId = templateVal.ToString();
        var roomId = roomVal.ToString();

        SpawnOverride? over = null;
        var ovVal = obj.Get("override");
        if (ovVal is ObjectInstance ov)
        {
            var maxHpVal = ov.Get("maxHp");
            var noRerollVal = ov.Get("noReroll");
            over = new SpawnOverride
            {
                FromType = AsStringOrNull(ov.Get("fromType")),
                Name = AsStringOrNull(ov.Get("name")),
                Desc = AsStringOrNull(ov.Get("desc")),
                MaxHp = maxHpVal.Type == Types.Number ? (int)(double)maxHpVal.ToObject()! : null,
                Damage = AsStringOrNull(ov.Get("damage")),
                NoReroll = noRerollVal.Type == Types.Boolean && (bool)noRerollVal.ToObject()!,
            };
            var items = ov.Get("items");
            if (items is JsArray arr)
            {
                for (uint i = 0; i < arr.Length; i++)
                {
                    var el = arr[(int)i];
                    if (el.Type != Types.Undefined && el.Type != Types.Null)
                    {
                        over.Items.Add(el.ToString());
                    }
                }
            }
        }

        var entity = _spawnManager.SpawnMob(templateId, roomId, over);
        if (entity == null) { return null; }
        return new { id = entity.Id.ToString(), name = entity.Name };
    }

    private static string? AsStringOrNull(JsValue v) =>
        v.Type == Types.String ? v.ToString() : null;
}
