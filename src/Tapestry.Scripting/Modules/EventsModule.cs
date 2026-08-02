using Jint.Native;
using Jint.Native.Object;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class EventsModule : IJintApiModule
{
    private readonly EventBus _eventBus;
    private readonly ILogger<EventsModule> _logger;

    public EventsModule(EventBus eventBus, ILogger<EventsModule>? logger = null)
    {
        _eventBus = eventBus;
        _logger = logger ?? NullLogger<EventsModule>.Instance;
    }

    public string Namespace => "events";

    public object Build(JintEngine engine)
    {
        return new
        {
            on = new Action<string, JsValue>((eventType, callback) =>
            {
                var dispatcher = new EventDispatcher(engine, callback, _logger);
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
        private readonly ILogger _logger;
        private readonly JsValue _cancelFn;
        private GameEvent? _current;

        public EventDispatcher(JintEngine engine, JsValue callback, ILogger logger)
        {
            _engine = engine;
            _callback = callback;
            _logger = logger;
            _cancelFn = JsValue.FromObject(engine, new Action(() =>
            {
                if (_current != null) { _current.Cancelled = true; }
            }));
        }

        public void Dispatch(GameEvent gameEvent)
        {
            // One Jint engine serves every pack and has no locking of its own, so a script
            // callback is only safe inside a tick. Publishing from a thread-pool thread (the
            // login sequence used to) races the loop's own invocation and corrupts Jint
            // mid-call -- which surfaces as an exception thrown from somewhere inside Jint
            // rather than from the pack, and takes the whole handler's effects with it. That
            // is silent to players and nearly invisible in a log, so name it here.
            if (LoopAffinity.LoopStarted && !LoopAffinity.OnLoop)
            {
                _logger.LogError(
                    "Script event dispatch off the game loop: eventType={EventType}. The publisher must " +
                    "post to GameLoop.Schedule instead; running here can tear a Jint call in progress.",
                    gameEvent.Type);
            }

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
