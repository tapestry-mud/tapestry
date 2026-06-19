using Jint.Native;
using Jint.Native.Object;
using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ScheduleModule : IJintApiModule
{
    private readonly GameLoop _gameLoop;
    private readonly World _world;
    private readonly Dictionary<string, int> _packCounters = new();
    private readonly Dictionary<string, List<string>> _packHandlers = new();

    public ScheduleModule(GameLoop gameLoop, World world)
    {
        _gameLoop = gameLoop;
        _world = world;
    }

    public string Namespace => "schedule";

    public void ResetPack(string packName)
    {
        if (_packHandlers.TryGetValue(packName, out var names))
        {
            foreach (var name in names)
            {
                _gameLoop.CancelTickHandler(name);
            }
        }
        _packHandlers[packName] = new List<string>();
        _packCounters[packName] = 0;
    }

    private string NextHandlerName(string packName)
    {
        var idx = _packCounters.GetValueOrDefault(packName, 0) + 1;
        _packCounters[packName] = idx;
        var name = $"{packName}:sched:{idx}";
        if (!_packHandlers.ContainsKey(packName))
        {
            _packHandlers[packName] = new List<string>();
        }
        _packHandlers[packName].Add(name);
        return name;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            every = new Func<int, JsValue, string>((ticks, fn) =>
            {
                var packName = engine.CurrentPackOwner();
                var name = NextHandlerName(packName);
                _gameLoop.RegisterTickHandler(name, ticks, () =>
                {
                    engine.Invoke(fn);
                }, packName);
                return name;
            }),

            everyForEach = new Func<int, JsValue, JsValue, string>((ticks, selectorVal, fn) =>
            {
                var packName = engine.CurrentPackOwner();
                var name = NextHandlerName(packName);

                var selectorObj = (ObjectInstance)selectorVal;
                var idVal   = selectorObj.Get("id");
                var typeVal = selectorObj.Get("type");
                var tagVal  = selectorObj.Get("tag");

                var spec = new Tapestry.Engine.Distribution.SelectorSpec(
                    Id:   idVal   != JsValue.Undefined ? idVal.ToString()   : null,
                    Type: typeVal != JsValue.Undefined ? typeVal.ToString() : null,
                    Tag:  tagVal  != JsValue.Undefined ? tagVal.ToString()  : null);

                _gameLoop.RegisterTickHandler(name, ticks, () =>
                {
                    var entities = Tapestry.Engine.Distribution.EntitySelector
                        .ResolveEntities(_world, spec);

                    foreach (var entity in entities.ToList())
                    {
                        var proxy = new { id = entity.Id.ToString(), name = entity.Name, type = entity.Type };
                        engine.Invoke(fn, JsValue.FromObject(engine, proxy));
                    }
                }, packName);
                return name;
            }),

            cancel = new Action<string>(handle =>
            {
                _gameLoop.CancelTickHandler(handle);
            })
        };
    }
}
