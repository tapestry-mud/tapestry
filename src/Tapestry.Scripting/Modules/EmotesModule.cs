using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class EmotesModule : IJintApiModule
{
    private readonly EmoteRegistry _emoteRegistry;
    private readonly RegistrationPolicy _registrationPolicy;

    public EmotesModule(EmoteRegistry emoteRegistry, RegistrationPolicy registrationPolicy)
    {
        _emoteRegistry = emoteRegistry;
        _registrationPolicy = registrationPolicy;
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

                var packNameVal = engine.GetValue("__currentPack");
                var packName = (packNameVal.Type != Types.Undefined && packNameVal.Type != Types.Null)
                    ? packNameVal.ToString() : "";

                var sourceFileVal = engine.GetValue("__currentSource");
                var sourceFile = (sourceFileVal.Type != Types.Undefined && sourceFileVal.Type != Types.Null)
                    ? sourceFileVal.ToString() : "";

                // Jint 4.7.1 has no IsBoolean; a missing JS field reads Undefined. Only Type==Boolean counts.
                var overrideVal = obj.Get("override");
                bool isOverride = overrideVal.Type == Types.Boolean && (bool)overrideVal.ToObject()!;

                // Declarative: the registry write replays at Resolve() (the seal barrier),
                // turning the silent last-wins clobber into a located boot error.
                _registrationPolicy.Record(new RegistrationCandidate(
                    Kind: "emote",
                    Name: name,
                    Owner: packName,
                    IsOverride: isOverride,
                    Commit: () => _emoteRegistry.Register(new EmoteDefinition
                    {
                        Name = name,
                        SelfMessage = selfMsg,
                        RoomMessage = roomMsg,
                        TargetMessage = targetMsg
                    }),
                    SourceFile: sourceFile,
                    Line: 0));
            })
        };
    }
}
