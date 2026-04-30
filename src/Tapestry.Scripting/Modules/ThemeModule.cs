using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine.Color;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ThemeModule : IJintApiModule
{
    private readonly ThemeRegistry _themeRegistry;

    public ThemeModule(ThemeRegistry themeRegistry)
    {
        _themeRegistry = themeRegistry;
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

                _themeRegistry.Register(tag, new ThemeEntry { Fg = fg, Bg = bg, Html = html });
            })
        };
    }
}
