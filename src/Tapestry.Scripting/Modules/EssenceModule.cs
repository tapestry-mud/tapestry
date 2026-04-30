using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine.Color;
using Tapestry.Engine.Inventory;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class EssenceModule : IJintApiModule
{
    private readonly EssenceRegistry _registry;
    private readonly ThemeRegistry _themeRegistry;

    public EssenceModule(EssenceRegistry registry, ThemeRegistry themeRegistry)
    {
        _registry = registry;
        _themeRegistry = themeRegistry;
    }

    public string Namespace => "essence";

    public object Build(JintEngine engine)
    {
        return new
        {
            register = new Action<JsValue>(def =>
            {
                var obj = (ObjectInstance)def;
                var key = obj.Get("key").ToString();
                var glyph = obj.Get("glyph").ToString();
                var color = obj.Get("color").ToString();
                _registry.Register(new EssenceDefinition(key, glyph, color));

                string? html = null;
                var htmlVal = obj.Get("html");
                if (htmlVal.Type != Types.Undefined && htmlVal.Type != Types.Null)
                {
                    html = htmlVal.ToString();
                }

                _themeRegistry.Register($"essence.{key}", new ThemeEntry { Fg = color, Html = html });
            }),

            format = new Func<string?, string>(essenceKey => _registry.Format(essenceKey)),

            getEssence = new Func<string, object?>(key =>
            {
                var essence = _registry.GetEssence(key);
                if (essence == null) { return null; }
                return new { key = essence.Key, glyph = essence.Glyph, color = essence.Color };
            })
        };
    }
}
