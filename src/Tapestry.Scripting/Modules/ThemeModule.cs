using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine.Color;
using Tapestry.Engine.Registration;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ThemeModule : IJintApiModule
{
    private readonly ThemeRegistry _themeRegistry;
    private readonly RegistrationPolicy _registrationPolicy;

    public ThemeModule(ThemeRegistry themeRegistry, RegistrationPolicy registrationPolicy)
    {
        _themeRegistry = themeRegistry;
        _registrationPolicy = registrationPolicy;
    }

    public string Namespace => "theme";

    public object Build(JintEngine engine)
    {
        return new
        {
            register = new Action<string, JsValue>((tag, def) =>
            {
                var obj = (ObjectInstance)def;

                string? fg = null;
                var fgVal = obj.Get("fg");
                if (fgVal.Type != Types.Undefined && fgVal.Type != Types.Null)
                {
                    fg = fgVal.ToString();
                }

                string? bg = null;
                var bgVal = obj.Get("bg");
                if (bgVal.Type != Types.Undefined && bgVal.Type != Types.Null)
                {
                    bg = bgVal.ToString();
                }

                string? html = null;
                var htmlVal = obj.Get("html");
                if (htmlVal.Type != Types.Undefined && htmlVal.Type != Types.Null)
                {
                    html = htmlVal.ToString();
                }

                var packNameVal = engine.GetValue("__currentPack");
                var packName = (packNameVal.Type != Types.Undefined && packNameVal.Type != Types.Null)
                    ? packNameVal.ToString() : "";

                var sourceFileVal = engine.GetValue("__currentSource");
                var sourceFile = (sourceFileVal.Type != Types.Undefined && sourceFileVal.Type != Types.Null)
                    ? sourceFileVal.ToString() : "";

                // Jint 4.7.1 has no IsBoolean; a missing JS field reads Undefined. Only Type==Boolean counts.
                var overrideVal = obj.Get("override");
                var isOverride = overrideVal.Type == Types.Boolean && (bool)overrideVal.ToObject()!;

                // Declarative: the registry write replays at Resolve() (the seal barrier),
                // turning the silent last-wins clobber into a located boot error.
                _registrationPolicy.Record(new RegistrationCandidate(
                    Kind: "theme",
                    Name: tag,
                    Owner: packName,
                    IsOverride: isOverride,
                    Commit: () => _themeRegistry.Register(tag, new ThemeEntry { Fg = fg, Bg = bg, Html = html }),
                    SourceFile: sourceFile,
                    Line: 0));
            })
        };
    }
}
