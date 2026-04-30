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
                _eventBus.Subscribe(eventType, gameEvent =>
                {
                    var capturedEvent = gameEvent;
                    var eventObj = new
                    {
                        type = gameEvent.Type,
                        sourceEntityId = gameEvent.SourceEntityId?.ToString(),
                        targetEntityId = gameEvent.TargetEntityId?.ToString(),
                        roomId = gameEvent.RoomId,
                        cancelled = gameEvent.Cancelled,
                        data = gameEvent.Data,
                        cancel = new Action(() => { capturedEvent.Cancelled = true; })
                    };

                    engine.Invoke(callback, JsValue.FromObject(engine, eventObj));
                });
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
}
