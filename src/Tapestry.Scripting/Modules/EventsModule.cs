using Jint.Native;
using Jint.Native.Object;
using Tapestry.Engine;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class EventsModule : IJintApiModule
{
    private readonly EventBus _eventBus;

    public EventsModule(EventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public string Namespace => "events";

    public object Build(JintEngine engine)
    {
        return new
        {
            on = new Action<string, JsValue>((eventType, callback) =>
            {
                var dispatcher = new EventDispatcher(engine, callback);
                _eventBus.Subscribe(eventType, dispatcher.Dispatch);
            }),

            publish = new Action<string, JsValue>((eventType, dataObj) =>
            {
                var data = new Dictionary<string, object?>();
                if (dataObj is ObjectInstance obj)
                {
                    foreach (var prop in obj.GetOwnProperties())
                    {
                        data[prop.Key.ToString()] = prop.Value.Value.ToObject();
                    }
                }

                _eventBus.Publish(new GameEvent
                {
                    Type = eventType,
                    Data = data
                });
            })
        };
    }

    private class EventDispatcher
    {
        private readonly JintEngine _engine;
        private readonly JsValue _callback;
        private readonly JsValue _cancelFn;
        private GameEvent? _current;

        public EventDispatcher(JintEngine engine, JsValue callback)
        {
            _engine = engine;
            _callback = callback;
            _cancelFn = JsValue.FromObject(engine, new Action(() =>
            {
                if (_current != null) { _current.Cancelled = true; }
            }));
        }

        public void Dispatch(GameEvent gameEvent)
        {
            _current = gameEvent;
            var jsEvent = _engine.Intrinsics.Object.Construct(Array.Empty<JsValue>());
            jsEvent.FastSetDataProperty("type", (JsValue)(gameEvent.Type));
            jsEvent.FastSetDataProperty("sourceEntityId", gameEvent.SourceEntityId?.ToString() ?? JsValue.Null);
            jsEvent.FastSetDataProperty("targetEntityId", gameEvent.TargetEntityId?.ToString() ?? JsValue.Null);
            jsEvent.FastSetDataProperty("roomId", gameEvent.RoomId ?? JsValue.Null);
            jsEvent.FastSetDataProperty("cancelled", gameEvent.Cancelled);
            jsEvent.FastSetDataProperty("data", JsValue.FromObject(_engine, gameEvent.Data));
            jsEvent.FastSetDataProperty("cancel", _cancelFn);
            _engine.Invoke(_callback, jsEvent);
        }
    }
}
