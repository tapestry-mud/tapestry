using Jint.Native;
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Mobs;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class MobsModule : IJintApiModule
{
    private readonly ApiMobs _mobs;
    private readonly MobAIManager _mobAIManager;
    private readonly ILogger<MobsModule> _logger;

    public MobsModule(ApiMobs mobs, MobAIManager mobAIManager, ILogger<MobsModule> logger)
    {
        _mobs = mobs;
        _mobAIManager = mobAIManager;
        _logger = logger;
    }

    public string Namespace => "mobs";

    public object Build(JintEngine engine)
    {
        return new
        {
            registerBehavior = new Action<string, JsValue>((name, handler) =>
            {
                _mobAIManager.RegisterBehavior(name, ctx =>
                {
                    var contextObj = new
                    {
                        entityId = ctx.EntityId.ToString(),
                        name = ctx.Name,
                        roomId = ctx.RoomId,
                        behavior = ctx.Behavior
                    };
                    engine.Invoke(handler, JsValue.FromObject(engine, contextObj));
                });
            }),
            getProperties = new Func<string, Dictionary<string, object?>?>(_mobs.GetEntityProperties),
            getTicksSinceLastAction = new Func<string, long>(_mobs.GetMobTicksSinceLastAction),
            recordAction = new Action<string>(_mobs.RecordMobAction),
            spawnMob = new Func<string, string, object?>(_mobs.SpawnMob)
        };
    }
}
