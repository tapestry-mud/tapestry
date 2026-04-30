using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class EmotesModule : IJintApiModule
{
    private readonly EmoteRegistry _emoteRegistry;

    public EmotesModule(EmoteRegistry emoteRegistry)
    {
        _emoteRegistry = emoteRegistry;
    }

    public string Namespace => "emotes";

    public object Build(JintEngine engine)
    {
        return new
        {
            register = new Action<JsValue>(definition =>
            {
                var obj = (ObjectInstance)definition;
                var name = obj.Get("name").ToString();
                var selfMsg = obj.Get("self").ToString();
                var roomMsg = obj.Get("room").ToString();

                string? targetMsg = null;
                var targetVal = obj.Get("target");
                if (targetVal.Type != Types.Undefined && targetVal.Type != Types.Null)
                {
                    targetMsg = targetVal.ToString();
                }

                _emoteRegistry.Register(new EmoteDefinition
                {
                    Name = name,
                    SelfMessage = selfMsg,
                    RoomMessage = roomMsg,
                    TargetMessage = targetMsg
                });
            })
        };
    }
}
