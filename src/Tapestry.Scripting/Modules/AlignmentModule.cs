using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class AlignmentModule : IJintApiModule
{
    private readonly AlignmentManager _manager;
    private readonly AlignmentConfig _config;
    private readonly World _world;

    public AlignmentModule(AlignmentManager manager, AlignmentConfig config, World world)
    {
        _manager = manager;
        _config = config;
        _world = world;
    }

    public string Namespace => "alignment";

    public object Build(JintEngine engine)
    {
        return new
        {
            get = new Func<string, int>((entityIdStr) =>
            {
                return Guid.TryParse(entityIdStr, out var id) ? _manager.Get(id) : 0;
            }),

            bucket = new Func<string, string>((entityIdStr) =>
            {
                return Guid.TryParse(entityIdStr, out var id) ? _manager.Bucket(id) : "neutral";
            }),

            history = new Func<string, object[]>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return Array.Empty<object>(); }
                return _manager.History(id)
                    .Select(h => (object)new
                    {
                        timestamp = h.Timestamp,
                        delta = h.Delta,
                        reason = h.Reason,
                        newValue = h.NewValue
                    })
                    .ToArray();
            }),

            set = new Action<string, int, string>((entityIdStr, value, reason) =>
            {
                if (Guid.TryParse(entityIdStr, out var id)) { _manager.Set(id, value, reason); }
            }),

            shift = new Action<string, int, string>((entityIdStr, delta, reason) =>
            {
                if (Guid.TryParse(entityIdStr, out var id)) { _manager.Shift(id, delta, reason); }
            }),

            configure = new Action<JsValue>((optionsVal) =>
            {
                if (optionsVal is not ObjectInstance options) { return; }
                var thresholdsVal = options.Get("thresholds");
                if (thresholdsVal is not ObjectInstance thresholds) { return; }
                var evilVal = thresholds.Get("evil");
                var goodVal = thresholds.Get("good");
                if (evilVal.Type == Types.Number && goodVal.Type == Types.Number)
                {
                    _config.Configure((int)(double)evilVal.ToObject()!, (int)(double)goodVal.ToObject()!);
                }
            }),

            setGender = new Action<string, string>((entityIdStr, gender) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return; }
                var entity = _world.GetEntity(id);
                entity?.SetProperty("gender", gender);
            }),

            getGender = new Func<string, object?>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return null; }
                return _world.GetEntity(id)?.GetProperty<string>("gender");
            })
        };
    }
}
